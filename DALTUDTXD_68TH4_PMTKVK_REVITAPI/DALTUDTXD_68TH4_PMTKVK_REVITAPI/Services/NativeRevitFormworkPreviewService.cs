using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services
{
    /// <summary>
    /// Builds a native Revit 3D preview. Structural geometry is never recreated: the
    /// dedicated View3D displays the live project model and only add-in-owned formwork
    /// DirectShapes are generated around it.
    /// </summary>
    public sealed class NativeRevitFormworkPreviewService
    {
        private const string PreviewViewName = "VK_PREVIEW_3D_NATIVE";
        private const string ApplicationId = "DALTUDTXD_68TH4_NATIVE_FORMWORK_PREVIEW";

        public sealed class Result
        {
            public ElementId ViewId { get; set; }
            public int StructuralElementCount { get; set; }
            public int FormworkShapeCount { get; set; }
            public int AccessoryFamilyInstanceCount { get; set; }
            public string Summary { get; set; }
        }

        public Result Build(Document document, IEnumerable<FloorElementModel> selectedComponents,
            SharedDataViewModel sharedData, bool detailedAccessories,
            Action<int, string> reportProgress = null, Func<bool> isCancellationRequested = null)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            ThrowIfCancelled(isCancellationRequested);
            reportProgress?.Invoke(2, "Đang đọc cấu kiện kết cấu từ Revit...");

            List<FloorElementModel> componentModels = (selectedComponents ??
                Enumerable.Empty<FloorElementModel>()).Where(model => model != null).ToList();
            List<(Element Element, StructuralComponentType Type)> components = ResolveComponents(
                document, componentModels).ToList();
            if (components.Count == 0)
                throw new InvalidOperationException("Không tìm thấy Sàn, Dầm, Cột hoặc Móng trong dự án Revit.");

            NativeRevitAccessoryFamilyService accessoryService = null;
            if (detailedAccessories)
            {
                var braceAssessmentBuilder = new ProjectFormworkPreviewBuilder(sharedData);
                reportProgress?.Invoke(5, "Đang phân tích hình học và vị trí chân chống...");
                braceAssessmentBuilder.RefreshColumnBraceAssessmentsOnly();
                ThrowIfCancelled(isCancellationRequested);
                accessoryService = new NativeRevitAccessoryFamilyService(document, sharedData, componentModels,
                    braceAssessmentBuilder.CoordinateOriginMm);
                reportProgress?.Invoke(8, "Đang chuẩn bị family phụ kiện...");
                accessoryService.EnsureDefinitions(components,
                    (done, message) => reportProgress?.Invoke(8 + (int)Math.Round(done * 0.12), message),
                    isCancellationRequested);
                reportProgress?.Invoke(20, "Đã chuẩn bị family; bắt đầu tạo ván khuôn...");
            }
            else
            {
                reportProgress?.Invoke(8,
                    "Preview nhanh: dùng DirectShape nhẹ cho cây chống, gông và ty ren...");
            }

            View3D previewView;
            int created = 0;
            int accessoryShapeCount = 0;
            var accessories = new NativeRevitAccessoryFamilyService.PlacementResult();
            NativeRevitQuickAccessoryService.BuildResult quickAccessoryResult = null;
            using (var transaction = new Transaction(document, "Cập nhật preview 3D ván khuôn Revit"))
            {
                transaction.Start();
                FailureHandlingOptions failureOptions = transaction.GetFailureHandlingOptions();
                failureOptions.SetFailuresPreprocessor(new PreviewFailurePreprocessor(document));
                failureOptions.SetClearAfterRollback(true);
                transaction.SetFailureHandlingOptions(failureOptions);
                previewView = GetOrCreatePreviewView(document);
                DeleteOwnedPreviewShapes(document);
                DeleteOwnedPreviewInstances(document);
                var previewElementIds = new List<ElementId>();

                FillPatternElement solidFill = new FilteredElementCollector(document)
                    .OfClass(typeof(FillPatternElement))
                    .Cast<FillPatternElement>()
                    .FirstOrDefault(item => item.GetFillPattern()?.IsSolidFill == true);
                OverrideGraphicSettings formworkGraphics = CreateFormworkGraphics(solidFill);
                OverrideGraphicSettings accessoryGraphics = CreateAccessoryGraphics(solidFill);
                var quickGeometryBatch = new List<GeometryObject>();
                int quickBatchNumber = 0;
                var quickAccessoryShapes = new List<DirectShape>();

                for (int componentIndex = 0; componentIndex < components.Count; componentIndex++)
                {
                    ThrowIfCancelled(isCancellationRequested);
                    (Element element, StructuralComponentType type) = components[componentIndex];
                    // The quick path intentionally avoids traversing arbitrary Family geometry.
                    // Invalid imported/symbol face loops can fail inside Revit's native geometry
                    // kernel before .NET receives an exception. Deterministic boxes are stable,
                    // low-cost, and sufficient for a whole-project navigation preview.
                    List<Solid> panels = detailedAccessories
                        ? BuildFormworkPanels(element, type, sharedData, true)
                        : BuildBoundingBoxFallback(element, type,
                            UnitUtils.ConvertToInternalUnits(18, UnitTypeId.Millimeters));
                    if (detailedAccessories)
                    {
                        BoundingBoxXYZ elementBox = element.get_BoundingBox(null);
                        IEnumerable<Element> touchingElements = components
                            .Where(item => item.Element.Id != element.Id &&
                                           BoxesOverlap(elementBox, item.Element.get_BoundingBox(null)))
                            .Select(item => item.Element);
                        panels = RevitFormworkService.SubtractIntersectingSolids(panels, touchingElements);
                    }
                    if (panels.Count == 0) continue;

                    if (detailedAccessories)
                    {
                        DirectShape shape = CreatePreviewShape(document, panels.Cast<GeometryObject>(),
                            $"{element.UniqueId}|{type}", $"VK Preview - {TypeLabel(type)} - {element.Id}");
                        previewView.SetElementOverrides(shape.Id, formworkGraphics);
                        previewElementIds.Add(shape.Id);
                        created++;
                    }
                    else
                    {
                        quickGeometryBatch.AddRange(panels.Cast<GeometryObject>());
                        // A small number of batched DirectShapes is substantially faster than
                        // creating one database element and one graphics override per component.
                        if (quickGeometryBatch.Count >= 96)
                        {
                            quickBatchNumber++;
                            DirectShape shape = CreatePreviewShape(document, quickGeometryBatch,
                                $"FAST|{quickBatchNumber}", $"VK Preview nhanh - cụm {quickBatchNumber}");
                            previewView.SetElementOverrides(shape.Id, formworkGraphics);
                            previewElementIds.Add(shape.Id);
                            quickGeometryBatch.Clear();
                            created++;
                        }
                    }
                    if (componentIndex % Math.Max(1, components.Count / 40) == 0 ||
                        componentIndex == components.Count - 1)
                    {
                        int percent = 20 + (int)Math.Round(48.0 * (componentIndex + 1) / components.Count);
                        reportProgress?.Invoke(percent,
                            $"Đang tạo ván khuôn: {componentIndex + 1}/{components.Count} cấu kiện...");
                    }
                }

                if (!detailedAccessories && quickGeometryBatch.Count > 0)
                {
                    quickBatchNumber++;
                    DirectShape shape = CreatePreviewShape(document, quickGeometryBatch,
                        $"FAST|{quickBatchNumber}", $"VK Preview nhanh - cụm {quickBatchNumber}");
                    previewView.SetElementOverrides(shape.Id, formworkGraphics);
                    previewElementIds.Add(shape.Id);
                    created++;
                }

                if (detailedAccessories)
                {
                    reportProgress?.Invoke(72, "Đang bố trí cây chống, gông cột và chống xiên...");
                    accessories = accessoryService.Place(components, previewView, accessoryGraphics,
                        (done, message) => reportProgress?.Invoke(72 + (int)Math.Round(done * 0.14), message),
                        isCancellationRequested);
                }
                else
                {
                    reportProgress?.Invoke(70,
                        "Đang dựng cây chống, gông cột và ty ren dạng nhẹ...");
                    var quickAccessoryService = new NativeRevitQuickAccessoryService(sharedData, componentModels);
                    quickAccessoryResult = quickAccessoryService.Build(components,
                        (done, message) => reportProgress?.Invoke(
                            70 + (int)Math.Round(done * 0.16), message), isCancellationRequested);
                    accessories = quickAccessoryResult.Counts;

                    const int accessoryBatchSize = 128;
                    for (int start = 0; start < quickAccessoryResult.Solids.Count;
                         start += accessoryBatchSize)
                    {
                        ThrowIfCancelled(isCancellationRequested);
                        accessoryShapeCount++;
                        List<GeometryObject> batch = quickAccessoryResult.Solids
                            .Skip(start).Take(accessoryBatchSize).Cast<GeometryObject>().ToList();
                        DirectShape shape = CreatePreviewShape(document, batch,
                            $"FAST_ACCESSORY|{accessoryShapeCount}",
                            $"VK Phụ kiện preview nhanh - cụm {accessoryShapeCount}");
                        previewView.SetElementOverrides(shape.Id, accessoryGraphics);
                        previewElementIds.Add(shape.Id);
                        quickAccessoryShapes.Add(shape);
                    }
                }

                List<FamilyInstance> accessoryInstances = new FilteredElementCollector(document)
                    .OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                    .Where(NativeRevitAccessoryFamilyService.IsOwnedPreviewInstance)
                    .ToList();
                previewElementIds.AddRange(accessoryInstances.Select(instance => instance.Id));
                ThrowIfCancelled(isCancellationRequested);
                // Revit is owned/disabled while the modal preview is open and all generated
                // items are deleted when the page closes. Hiding every item in every project
                // view was O(view count × item count) and caused the apparent application hang.
                reportProgress?.Invoke(88, "Đang thiết lập view Preview3D tạm thời...");
                reportProgress?.Invoke(92, "Đang thiết lập camera và phạm vi hiển thị...");
                // DirectShape and FamilyInstance bounding boxes are not guaranteed to
                // exist until regeneration. Resolve them before calculating the section
                // box so long props below the lowest slab are not clipped out of view.
                document.Regenerate();
                ConfigurePreviewView(previewView,
                    components.Select(item => item.Element)
                        .Concat(accessoryInstances.Cast<Element>())
                        .Concat(quickAccessoryShapes.Cast<Element>()));
                document.Regenerate();
                reportProgress?.Invoke(96, "Đang hoàn tất transaction Revit...");
                ThrowIfCancelled(isCancellationRequested);
                transaction.Commit();
            }

            reportProgress?.Invoke(100, "Hoàn tất mô hình 3D.");

            string accessoryKind = detailedAccessories ? "family" : "DirectShape nhanh";
            string quickCapText = quickAccessoryResult?.IsDisplayCapped == true
                ? $" · LOD phụ kiện đã dừng ở {quickAccessoryResult.Solids.Count:N0} khối để bảo vệ hiệu năng"
                : string.Empty;
            string quickWarningText = quickAccessoryResult != null && quickAccessoryResult.Warnings.Count > 0
                ? $" · bỏ qua {quickAccessoryResult.Warnings.Count} cấu kiện phụ kiện lỗi hình học"
                : string.Empty;
            return new Result
            {
                ViewId = previewView.Id,
                StructuralElementCount = components.Count,
                FormworkShapeCount = created,
                AccessoryFamilyInstanceCount = accessories.Total,
                Summary = (detailedAccessories ? "Mô hình chi tiết" : "Preview nhanh") +
                          $" · Revit nguyên bản · {components.Count} cấu kiện kết cấu · " +
                          $"{created} cụm ván khuôn · {accessoryShapeCount} cụm phụ kiện · " +
                          $"{accessories.PropCount} cây chống {accessoryKind} · " +
                          $"{accessories.ScaffoldBraceCount} cụm giằng X giàn giáo {accessoryKind} · " +
                          $"{accessories.BeamClampCount} gông ty dầm {accessoryKind} · " +
                          $"{accessories.ColumnYokeCount} gông cột {accessoryKind} · " +
                          $"{accessories.ColumnTieRodCount} ty ren + bát chuồn {accessoryKind} · " +
                          $"{accessories.PushPullPropCount} chống xiên {accessoryKind} · " +
                          $"{accessories.TurnbuckleCount} cáp + tăng đơ {accessoryKind} · view {PreviewViewName}" +
                          quickCapText +
                          quickWarningText +
                          (accessoryService == null || accessoryService.Warnings.Count == 0
                              ? string.Empty
                              : $" · cảnh báo {accessoryService.Warnings.Count} family không nạp được")
            };
        }

        private static void ThrowIfCancelled(Func<bool> isCancellationRequested)
        {
            if (isCancellationRequested?.Invoke() == true)
                throw new OperationCanceledException("Người dùng đã hủy dựng mô hình 3D.");
        }

        private static DirectShape CreatePreviewShape(Document document,
            IEnumerable<GeometryObject> geometry, string dataId, string name)
        {
            var shape = DirectShape.CreateElement(document,
                new ElementId((long)BuiltInCategory.OST_GenericModel));
            shape.ApplicationId = ApplicationId;
            shape.ApplicationDataId = dataId;
            shape.Name = name;
            shape.SetShape(geometry.ToList());
            return shape;
        }

        /// <summary>
        /// Removes generated instances and DirectShapes when the preview page is left.
        /// Family definitions remain available for reuse/editing, but no generated
        /// accessory or formwork instance is retained in the user's project model.
        /// </summary>
        public static void RemovePreviewArtifacts(Document document)
        {
            if (document == null || document.IsReadOnly || document.IsModifiable) return;
            List<ElementId> ids = OwnedPreviewElementIds(document);
            if (ids.Count == 0) return;
            using (var transaction = new Transaction(document, "Dọn phần tử tạm của Preview 3D"))
            {
                transaction.Start();
                document.Delete(ids);
                transaction.Commit();
            }
        }

        private static IEnumerable<(Element Element, StructuralComponentType Type)> ResolveComponents(
            Document document, IEnumerable<FloorElementModel> selectedComponents)
        {
            var resolved = new Dictionary<long, (Element, StructuralComponentType)>();
            foreach (FloorElementModel model in selectedComponents ?? Enumerable.Empty<FloorElementModel>())
            {
                if (model == null) continue;
                Element element = !string.IsNullOrWhiteSpace(model.UniqueId)
                    ? document.GetElement(model.UniqueId)
                    : long.TryParse(model.ElementId, out long idValue)
                        ? document.GetElement(new ElementId(idValue))
                        : null;
                if (element != null) resolved[element.Id.Value] = (element, model.ComponentType);
            }

            if (resolved.Count > 0) return resolved.Values;

            var categories = new[]
            {
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFoundation
            };
            foreach (Element element in new FilteredElementCollector(document)
                         .WherePasses(new ElementMulticategoryFilter(categories))
                         .WhereElementIsNotElementType())
            {
                StructuralComponentType type = Classify(document, element);
                resolved[element.Id.Value] = (element, type);
            }
            return resolved.Values;
        }

        private static StructuralComponentType Classify(Document document, Element element)
        {
            BuiltInCategory category = element.Category?.BuiltInCategory ?? BuiltInCategory.INVALID;
            if (category == BuiltInCategory.OST_StructuralFraming) return StructuralComponentType.Beam;
            if (category == BuiltInCategory.OST_StructuralColumns) return StructuralComponentType.Column;
            if (category == BuiltInCategory.OST_StructuralFoundation) return StructuralComponentType.Foundation;
            if (element is Floor floor && document.GetElement(floor.GetTypeId()) is FloorType floorType &&
                floorType.IsFoundationSlab) return StructuralComponentType.Foundation;
            return StructuralComponentType.Slab;
        }

        private static View3D GetOrCreatePreviewView(Document document)
        {
            View3D existing = new FilteredElementCollector(document)
                .OfClass(typeof(View3D)).Cast<View3D>()
                .FirstOrDefault(view => !view.IsTemplate && view.Name == PreviewViewName);
            // Reuse the dedicated view. Deleting it is unsafe if a native PreviewControl
            // from a previous window has not completed its Unloaded lifecycle yet.
            if (existing != null) return existing;

            ViewFamilyType type = new FilteredElementCollector(document)
                .OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>()
                .First(item => item.ViewFamily == ViewFamily.ThreeDimensional);
            View3D created = View3D.CreateIsometric(document, type.Id);
            created.Name = PreviewViewName;
            return created;
        }

        private static void DeleteOwnedPreviewShapes(Document document)
        {
            List<ElementId> ownedIds = new FilteredElementCollector(document)
                .OfClass(typeof(DirectShape)).Cast<DirectShape>()
                .Where(shape => string.Equals(shape.ApplicationId, ApplicationId, StringComparison.Ordinal))
                .Select(shape => shape.Id).ToList();
            if (ownedIds.Count > 0) document.Delete(ownedIds);
        }

        private static void DeleteOwnedPreviewInstances(Document document)
        {
            List<ElementId> ownedIds = new FilteredElementCollector(document)
                .OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                .Where(NativeRevitAccessoryFamilyService.IsPreviewFamilyInstance)
                .Select(instance => instance.Id).ToList();
            if (ownedIds.Count > 0) document.Delete(ownedIds);
        }

        private static List<ElementId> OwnedPreviewElementIds(Document document)
        {
            var ids = new HashSet<ElementId>(new ElementIdComparer());
            foreach (DirectShape shape in new FilteredElementCollector(document)
                         .OfClass(typeof(DirectShape)).Cast<DirectShape>()
                         .Where(shape => string.Equals(shape.ApplicationId, ApplicationId,
                             StringComparison.Ordinal)))
                ids.Add(shape.Id);
            foreach (FamilyInstance instance in new FilteredElementCollector(document)
                         .OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>()
                         .Where(NativeRevitAccessoryFamilyService.IsPreviewFamilyInstance))
                ids.Add(instance.Id);
            return ids.ToList();
        }

        private static void RestrictPreviewElementsToDedicatedView(Document document, View3D previewView,
            ICollection<ElementId> previewElementIds)
        {
            if (previewElementIds == null || previewElementIds.Count == 0) return;
            foreach (View view in new FilteredElementCollector(document).OfClass(typeof(View)).Cast<View>())
            {
                if (view.IsTemplate || view.Id == previewView.Id) continue;
                List<ElementId> hideable = previewElementIds
                    .Where(id => document.GetElement(id)?.CanBeHidden(view) == true)
                    .ToList();
                if (hideable.Count == 0) continue;
                try { view.HideElements(hideable); }
                catch (Autodesk.Revit.Exceptions.ArgumentException)
                {
                    // Some system/non-graphical views reject a mixed batch. Hide only
                    // individually valid elements without interrupting preview creation.
                    foreach (ElementId id in hideable)
                    {
                        try { view.HideElements(new List<ElementId> { id }); } catch { }
                    }
                }
            }
        }

        private sealed class ElementIdComparer : IEqualityComparer<ElementId>
        {
            public bool Equals(ElementId left, ElementId right) => left?.Value == right?.Value;
            public int GetHashCode(ElementId id) => id?.Value.GetHashCode() ?? 0;
        }

        /// <summary>
        /// Preview geometry is disposable and never drives schedules. Revit warnings
        /// such as duplicate-instance notifications must not interrupt the user. Errors
        /// are intentionally left untouched and still roll back the transaction.
        /// </summary>
        private sealed class PreviewFailurePreprocessor : IFailuresPreprocessor
        {
            private readonly Document _document;

            public PreviewFailurePreprocessor(Document document)
            {
                _document = document;
            }

            public FailureProcessingResult PreprocessFailures(FailuresAccessor failuresAccessor)
            {
                bool removedInvalidPreviewElement = false;
                foreach (FailureMessageAccessor message in failuresAccessor.GetFailureMessages())
                {
                    if (message.GetSeverity() == FailureSeverity.Warning)
                    {
                        failuresAccessor.DeleteWarning(message);
                        continue;
                    }

                    // A malformed generated accessory must never open Revit's blocking
                    // "errors must be addressed" dialog. Remove only add-in-owned family
                    // candidates; structural/project elements are never touched here.
                    List<ElementId> failingIds = message.GetFailingElementIds().ToList();
                    if (failingIds.Count > 0 && failingIds.All(id =>
                            _document.GetElement(id) is FamilyInstance instance &&
                            NativeRevitAccessoryFamilyService.IsPreviewFamilyInstance(instance)))
                    {
                        failuresAccessor.DeleteElements(failingIds);
                        removedInvalidPreviewElement = true;
                    }
                }
                return removedInvalidPreviewElement
                    ? FailureProcessingResult.ProceedWithCommit
                    : FailureProcessingResult.Continue;
            }
        }

        private static OverrideGraphicSettings CreateFormworkGraphics(FillPatternElement solidFill)
        {
            var graphics = new OverrideGraphicSettings();
            var face = new Autodesk.Revit.DB.Color(205, 145, 72);
            var edge = new Autodesk.Revit.DB.Color(92, 55, 20);
            graphics.SetProjectionLineColor(edge);
            graphics.SetProjectionLineWeight(3);
            graphics.SetSurfaceTransparency(10);
            if (solidFill != null)
            {
                graphics.SetSurfaceForegroundPatternId(solidFill.Id);
                graphics.SetSurfaceForegroundPatternColor(face);
            }
            return graphics;
        }

        private static OverrideGraphicSettings CreateAccessoryGraphics(FillPatternElement solidFill)
        {
            var graphics = new OverrideGraphicSettings();
            var face = new Autodesk.Revit.DB.Color(45, 105, 175);
            graphics.SetProjectionLineColor(new Autodesk.Revit.DB.Color(20, 65, 120));
            graphics.SetProjectionLineWeight(3);
            graphics.SetSurfaceTransparency(4);
            if (solidFill != null)
            {
                graphics.SetSurfaceForegroundPatternId(solidFill.Id);
                graphics.SetSurfaceForegroundPatternColor(face);
            }
            return graphics;
        }

        private static void ConfigurePreviewView(View3D view, IEnumerable<Element> elements)
        {
            try { view.ViewTemplateId = ElementId.InvalidElementId; } catch { }
            try
            {
                foreach (ElementId filterId in view.GetFilters().ToList())
                    view.RemoveFilter(filterId);
            }
            catch { }
            BuiltInCategory[] visibleCategories =
            {
                BuiltInCategory.OST_GenericModel,
                BuiltInCategory.OST_Floors,
                BuiltInCategory.OST_StructuralFraming,
                BuiltInCategory.OST_StructuralColumns,
                BuiltInCategory.OST_StructuralFoundation
            };
            foreach (BuiltInCategory category in visibleCategories)
            {
                try { view.SetCategoryHidden(new ElementId((long)category), false); } catch { }
            }
            try { view.DetailLevel = ViewDetailLevel.Fine; } catch { }
            try { view.DisplayStyle = DisplayStyle.Shading; } catch { }

            List<BoundingBoxXYZ> boxes = elements.Select(element => element.get_BoundingBox(null))
                .Where(box => box != null).ToList();
            if (boxes.Count == 0) return;
            double margin = UnitUtils.ConvertToInternalUnits(1200, UnitTypeId.Millimeters);
            var section = new BoundingBoxXYZ
            {
                Min = new XYZ(boxes.Min(box => box.Min.X) - margin,
                    boxes.Min(box => box.Min.Y) - margin, boxes.Min(box => box.Min.Z) - margin),
                Max = new XYZ(boxes.Max(box => box.Max.X) + margin,
                    boxes.Max(box => box.Max.Y) + margin, boxes.Max(box => box.Max.Z) + margin)
            };
            try
            {
                view.IsSectionBoxActive = true;
                view.SetSectionBox(section);
            }
            catch { }

            // A duplicated/existing Revit view can retain a camera aimed far outside
            // the new section box. Recompute an isometric orientation from project bounds.
            XYZ center = (section.Min + section.Max) * 0.5;
            double extent = Math.Max(section.Max.X - section.Min.X,
                Math.Max(section.Max.Y - section.Min.Y, section.Max.Z - section.Min.Z));
            XYZ forward = new XYZ(-1, -1, -0.72).Normalize();
            XYZ up = (XYZ.BasisZ - forward.Multiply(XYZ.BasisZ.DotProduct(forward))).Normalize();
            XYZ eye = center - forward.Multiply(Math.Max(extent * 2.2,
                UnitUtils.ConvertToInternalUnits(10000, UnitTypeId.Millimeters)));
            try { view.SetOrientation(new ViewOrientation3D(eye, up, forward)); } catch { }
        }

        private static List<Solid> BuildFormworkPanels(Element element, StructuralComponentType type,
            SharedDataViewModel sharedData, bool includeDetailedMembers)
        {
            double thickness = UnitUtils.ConvertToInternalUnits(18, UnitTypeId.Millimeters);
            var panels = new List<Solid>();
            GeometryElement geometry = element.get_Geometry(new Options
            {
                ComputeReferences = false,
                IncludeNonVisibleObjects = false,
                DetailLevel = ViewDetailLevel.Fine
            });

            if (geometry != null)
            {
                foreach (Solid solid in GetElementSolids(geometry, Transform.Identity))
                {
                    foreach (Face face in solid.Faces)
                    {
                        if (!(face is PlanarFace planarFace)) continue;
                        XYZ normal = planarFace.FaceNormal.Normalize();
                        if (!RequiresFormwork(type, normal)) continue;

                        try
                        {
                            IList<CurveLoop> loops = planarFace.GetEdgesAsCurveLoops();
                            if (loops.Count == 0) continue;
                            Solid panel = GeometryCreationUtilities.CreateExtrusionGeometry(
                                loops, normal, thickness);
                            if (panel != null && panel.Volume > 1e-9) panels.Add(panel);
                        }
                        catch (Autodesk.Revit.Exceptions.ApplicationException)
                        {
                            // A few imported or highly tessellated families expose invalid
                            // planar loops. Their bounding box is handled by the fallback below.
                        }
                    }
                }
            }

            if (panels.Count == 0)
                panels = BuildBoundingBoxFallback(element, type, thickness);

            // The beam panel family in the tutorial contains its stiffening members,
            // not plywood alone. Carry the effective per-element design into the native
            // Revit preview so auto-designed spacing/sections remain visible.
            if (includeDetailedMembers && type == StructuralComponentType.Beam)
                AddBeamPanelStiffeners(element, sharedData, panels);
            return panels;
        }

        private static void AddBeamPanelStiffeners(Element element, SharedDataViewModel sharedData,
            ICollection<Solid> solids)
        {
            if (sharedData == null || solids == null ||
                !(element.Location is LocationCurve location) || location.Curve == null) return;
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return;

            Curve curve = location.Curve;
            XYZ start = curve.GetEndPoint(0);
            XYZ end = curve.GetEndPoint(1);
            XYZ axis = new XYZ(end.X - start.X, end.Y - start.Y, 0);
            if (axis.GetLength() < 1e-6) return;
            axis = axis.Normalize();
            XYZ cross = new XYZ(-axis.Y, axis.X, 0).Normalize();

            CalculationParameters design = sharedData.GetComponentFormworkDesign(element.UniqueId)?.Parameters;
            double MmValue(double value) => UnitUtils.ConvertToInternalUnits(value, UnitTypeId.Millimeters);
            double beamWidth = design?.BeamWidth > 0
                ? MmValue(design.BeamWidth)
                : Math.Abs(cross.X) * (box.Max.X - box.Min.X) +
                  Math.Abs(cross.Y) * (box.Max.Y - box.Min.Y);
            double beamDepth = design?.BeamDepth > 0
                ? MmValue(design.BeamDepth)
                : box.Max.Z - box.Min.Z;
            double bottomPly = MmValue(Math.Max(9,
                design?.BeamBottomPlywoodThickness ?? sharedData.BeamBottomPlywoodThickness));
            double sidePly = MmValue(Math.Max(9,
                design?.BeamSidePlywoodThickness ?? sharedData.BeamSidePlywoodThickness));
            double spacing = MmValue(Math.Max(75,
                design?.BeamBottomYokeSpacing ?? sharedData.BeamBottomYokeSpacing));
            double joistWidth = MmValue(Math.Max(25,
                design?.BeamSecondaryJoistWidth ?? sharedData.BeamSecondaryJoistWidth));
            double joistDepth = MmValue(Math.Max(25,
                design?.BeamSecondaryJoistDepth ?? sharedData.BeamSecondaryJoistDepth));
            double sideSpacing = MmValue(Math.Max(75,
                design?.BeamSideYokeSpacing ?? sharedData.BeamSideYokeSpacing));

            int bottomStations = Math.Max(2, (int)Math.Ceiling(curve.Length / spacing) + 1);
            for (int index = 0; index < bottomStations; index++)
            {
                double t = (double)index / (bottomStations - 1);
                XYZ point = curve.Evaluate(t, true);
                XYZ center = new XYZ(point.X, point.Y, box.Min.Z - bottomPly - joistDepth / 2);
                solids.Add(CreateOrientedBox(center, axis, cross, XYZ.BasisZ,
                    joistWidth, beamWidth + 2 * sidePly + MmValue(300), joistDepth));
            }

            int sideStations = Math.Max(2, (int)Math.Ceiling(curve.Length / sideSpacing) + 1);
            for (int index = 0; index < sideStations; index++)
            {
                double t = (double)index / (sideStations - 1);
                XYZ point = curve.Evaluate(t, true);
                double sideOffset = beamWidth / 2 + sidePly + joistDepth / 2;
                double centerZ = box.Min.Z + beamDepth / 2;
                foreach (double sign in new[] { -1.0, 1.0 })
                {
                    XYZ center = new XYZ(point.X, point.Y, centerZ) + cross * (sign * sideOffset);
                    solids.Add(CreateOrientedBox(center, axis, cross, XYZ.BasisZ,
                        joistWidth, joistDepth, beamDepth));
                }
            }
        }

        private static Solid CreateOrientedBox(XYZ center, XYZ xAxis, XYZ yAxis, XYZ zAxis,
            double xSize, double ySize, double zSize)
        {
            Solid local = CreateBox(-xSize / 2, -ySize / 2, -zSize / 2, xSize, ySize, zSize);
            Transform transform = Transform.Identity;
            transform.Origin = center;
            transform.BasisX = xAxis;
            transform.BasisY = yAxis;
            transform.BasisZ = zAxis;
            return SolidUtils.CreateTransformed(local, transform);
        }

        private static bool RequiresFormwork(StructuralComponentType type, XYZ normal)
        {
            const double verticalTolerance = 0.30;
            bool bottomFace = normal.Z < -0.70;
            bool verticalFace = Math.Abs(normal.Z) < verticalTolerance;

            switch (type)
            {
                case StructuralComponentType.Slab:
                    return bottomFace || verticalFace;
                case StructuralComponentType.Beam:
                    return bottomFace || verticalFace;
                case StructuralComponentType.Column:
                case StructuralComponentType.Foundation:
                    return verticalFace;
                default:
                    return false;
            }
        }

        private static IEnumerable<Solid> GetElementSolids(GeometryElement geometry, Transform transform)
        {
            foreach (GeometryObject item in geometry)
            {
                if (item is Solid solid && solid.Faces.Size > 0 && solid.Volume > 1e-9)
                {
                    Solid transformed = transform.IsIdentity
                        ? solid
                        : SolidUtils.CreateTransformed(solid, transform);
                    yield return transformed;
                }
                else if (item is GeometryInstance instance)
                {
                    Transform nested = transform.Multiply(instance.Transform);
                    foreach (Solid child in GetElementSolids(instance.GetSymbolGeometry(), nested))
                        yield return child;
                }
            }
        }

        private static List<Solid> BuildBoundingBoxFallback(Element element,
            StructuralComponentType type, double t)
        {
            BoundingBoxXYZ box = element.get_BoundingBox(null);
            if (box == null) return new List<Solid>();
            double dx = box.Max.X - box.Min.X;
            double dy = box.Max.Y - box.Min.Y;
            double dz = box.Max.Z - box.Min.Z;
            if (dx <= 1e-6 || dy <= 1e-6 || dz <= 1e-6) return new List<Solid>();

            var solids = new List<Solid>();
            if (type == StructuralComponentType.Slab)
            {
                solids.Add(CreateBox(box.Min.X, box.Min.Y, box.Min.Z - t, dx, dy, t));
                return solids;
            }

            if (type == StructuralComponentType.Beam)
            {
                solids.Add(CreateBox(box.Min.X, box.Min.Y, box.Min.Z - t, dx, dy, t));
                if (dx >= dy)
                {
                    solids.Add(CreateBox(box.Min.X, box.Min.Y - t, box.Min.Z, dx, t, dz));
                    solids.Add(CreateBox(box.Min.X, box.Max.Y, box.Min.Z, dx, t, dz));
                }
                else
                {
                    solids.Add(CreateBox(box.Min.X - t, box.Min.Y, box.Min.Z, t, dy, dz));
                    solids.Add(CreateBox(box.Max.X, box.Min.Y, box.Min.Z, t, dy, dz));
                }
                return solids;
            }

            // Columns and foundations receive four exposed side panels. Top faces stay
            // open for monolithic concrete placement and therefore are not formworked.
            solids.Add(CreateBox(box.Min.X - t, box.Min.Y, box.Min.Z, t, dy, dz));
            solids.Add(CreateBox(box.Max.X, box.Min.Y, box.Min.Z, t, dy, dz));
            solids.Add(CreateBox(box.Min.X, box.Min.Y - t, box.Min.Z, dx, t, dz));
            solids.Add(CreateBox(box.Min.X, box.Max.Y, box.Min.Z, dx, t, dz));
            return solids;
        }

        private static bool BoxesOverlap(BoundingBoxXYZ first, BoundingBoxXYZ second)
        {
            if (first == null || second == null) return false;
            double tolerance = UnitUtils.ConvertToInternalUnits(25, UnitTypeId.Millimeters);
            return first.Min.X - tolerance <= second.Max.X && first.Max.X + tolerance >= second.Min.X &&
                   first.Min.Y - tolerance <= second.Max.Y && first.Max.Y + tolerance >= second.Min.Y &&
                   first.Min.Z - tolerance <= second.Max.Z && first.Max.Z + tolerance >= second.Min.Z;
        }

        private static Solid CreateBox(double x, double y, double z, double dx, double dy, double dz)
        {
            var loop = new CurveLoop();
            XYZ p0 = new XYZ(x, y, z);
            XYZ p1 = new XYZ(x + dx, y, z);
            XYZ p2 = new XYZ(x + dx, y + dy, z);
            XYZ p3 = new XYZ(x, y + dy, z);
            loop.Append(Line.CreateBound(p0, p1));
            loop.Append(Line.CreateBound(p1, p2));
            loop.Append(Line.CreateBound(p2, p3));
            loop.Append(Line.CreateBound(p3, p0));
            return GeometryCreationUtilities.CreateExtrusionGeometry(
                new List<CurveLoop> { loop }, XYZ.BasisZ, dz);
        }

        private static string TypeLabel(StructuralComponentType type)
        {
            switch (type)
            {
                case StructuralComponentType.Foundation: return "Móng";
                case StructuralComponentType.Beam: return "Dầm";
                case StructuralComponentType.Column: return "Cột";
                default: return "Sàn";
            }
        }
    }
}
