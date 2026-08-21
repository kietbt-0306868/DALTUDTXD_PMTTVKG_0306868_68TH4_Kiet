using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services
{
    public sealed class FormworkWorkflowResult
    {
        public int Processed { get; set; }
        public int Changed { get; set; }
        public int Skipped { get; set; }
        public List<string> Warnings { get; } = new List<string>();
        public string Summary { get; set; }
    }

    /// <summary>
    /// Implements the Revit-side quantity-takeoff preparation shown in the BIMSpeed workflow:
    /// join, name, bind shared parameters, initialize calculation data, create schedules and
    /// publish net formwork areas. All operations are restricted to the selected components.
    /// </summary>
    public sealed class FormworkTakeoffWorkflowService
    {
        public const string NameParameter = "VK_TenCauKien";
        public const string GroupParameter = "VK_NhomThiCong";
        public const string SequenceParameter = "VK_TrinhTuThiCong";
        public const string TypeParameter = "VK_LoaiCauKien";
        public const string NetAreaParameter = "VK_DienTichRong";
        public const string BottomAreaParameter = "VK_DienTichDay";
        public const string SideAreaParameter = "VK_DienTichThanh";
        public const string TopAreaParameter = "VK_DienTichDinh";
        public const string IntersectionsParameter = "VK_GiaoCauKien";
        public const string ComplianceParameter = "VK_TrangThaiThietKe";
        public const string RevisionParameter = "VK_PhienBanThietKe";

        private static readonly string[] TextParameters =
        {
            NameParameter, GroupParameter, SequenceParameter, TypeParameter,
            IntersectionsParameter, ComplianceParameter, RevisionParameter
        };

        private static readonly string[] AreaParameters =
        {
            NetAreaParameter, BottomAreaParameter, SideAreaParameter, TopAreaParameter
        };

        public FormworkWorkflowResult EnsureSharedParameters(Document document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            Application app = document.Application;
            string previousFile = app.SharedParametersFilename;
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "DALTUDTXD_Formwork");
            string parameterFile = Path.Combine(folder, "FormworkSharedParameters.txt");
            Directory.CreateDirectory(folder);
            EnsureSharedParameterFile(parameterFile);

            var result = new FormworkWorkflowResult();
            try
            {
                app.SharedParametersFilename = parameterFile;
                DefinitionFile file = app.OpenSharedParameterFile();
                if (file == null) throw new InvalidOperationException("Không thể mở tệp Shared Parameters của mô-đun ván khuôn.");
                DefinitionGroup group = file.Groups.get_Item("VAN_KHUON") ?? file.Groups.Create("VAN_KHUON");

                using (var transaction = new Transaction(document, "VK - Nạp Shared Parameters"))
                {
                    transaction.Start();
                    foreach (string name in TextParameters)
                    {
                        ExternalDefinition definition = GetOrCreateDefinition(group, name, SpecTypeId.String.Text);
                        if (BindDefinition(document, definition)) result.Changed++;
                        result.Processed++;
                    }
                    foreach (string name in AreaParameters)
                    {
                        ExternalDefinition definition = GetOrCreateDefinition(group, name, SpecTypeId.Area);
                        if (BindDefinition(document, definition)) result.Changed++;
                        result.Processed++;
                    }
                    transaction.Commit();
                }
            }
            finally
            {
                app.SharedParametersFilename = previousFile;
            }

            result.Summary = $"Đã nạp {result.Processed} tham số VK_*; {result.Changed} liên kết được tạo/cập nhật.";
            return result;
        }

        public FormworkWorkflowResult NameComponents(Document document, IList<FloorElementModel> components)
        {
            EnsureSelected(document, components);
            EnsureSharedParameters(document);
            var ordered = ResolveComponents(document, components)
                .OrderBy(item => GetLevelElevation(document, item.Element))
                .ThenBy(item => GetPriority(item.Model.ComponentType))
                .ThenBy(item => item.Element.Id.Value)
                .ToList();
            var levelOrder = ordered.Select(item => item.Model.Level ?? "Không xác định")
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select((level, index) => new { level, index = index + 1 })
                .ToDictionary(item => item.level, item => item.index, StringComparer.OrdinalIgnoreCase);
            var counters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var result = new FormworkWorkflowResult();

            using (var transaction = new Transaction(document, "VK - Đặt tên cấu kiện"))
            {
                transaction.Start();
                foreach (var item in ordered)
                {
                    string level = item.Model.Level ?? "Không xác định";
                    string code = GetComponentCode(item.Model.ComponentType);
                    string key = level + "|" + code;
                    counters.TryGetValue(key, out int number);
                    counters[key] = ++number;
                    string group = $"T{levelOrder[level]:00} - {level}";
                    string sequence = $"{levelOrder[level]:00}.{GetPriority(item.Model.ComponentType):00}";
                    string name = $"VK-{Sanitize(level)}-{code}-{number:000}";

                    SetText(item.Element, NameParameter, name);
                    SetText(item.Element, GroupParameter, group);
                    SetText(item.Element, SequenceParameter, sequence);
                    SetText(item.Element, TypeParameter, GetComponentLabel(item.Model.ComponentType));
                    item.Model.FormworkName = name;
                    item.Model.ConstructionGroup = group;
                    item.Model.ConstructionSequence = sequence;
                    item.Model.WorkflowStatus = "Đã đặt tên";
                    result.Processed++;
                    result.Changed++;
                }
                transaction.Commit();
            }

            result.Summary = $"Đã đặt tên {result.Changed} cấu kiện theo tầng, loại và thứ tự thi công.";
            return result;
        }

        public FormworkWorkflowResult JoinByConstructionSequence(Document document, IList<FloorElementModel> components)
        {
            EnsureSelected(document, components);
            List<(FloorElementModel Model, Element Element)> items = ResolveComponents(document, components).ToList();
            var result = new FormworkWorkflowResult();

            using (var transaction = new Transaction(document, "VK - Join theo trình tự thi công"))
            {
                transaction.Start();
                for (int i = 0; i < items.Count; i++)
                {
                    for (int j = i + 1; j < items.Count; j++)
                    {
                        var a = items[i];
                        var b = items[j];
                        if (a.Model.ComponentType == b.Model.ComponentType) continue;
                        if (!BoundsOverlap(a.Element.get_BoundingBox(null), b.Element.get_BoundingBox(null))) continue;
                        result.Processed++;
                        try
                        {
                            Element earlier = GetPriority(a.Model.ComponentType) <= GetPriority(b.Model.ComponentType)
                                ? a.Element : b.Element;
                            Element later = earlier.Id == a.Element.Id ? b.Element : a.Element;
                            if (!JoinGeometryUtils.AreElementsJoined(document, earlier, later))
                            {
                                JoinGeometryUtils.JoinGeometry(document, earlier, later);
                                result.Changed++;
                            }
                            if (JoinGeometryUtils.AreElementsJoined(document, earlier, later) &&
                                !JoinGeometryUtils.IsCuttingElementInJoin(document, earlier, later))
                            {
                                JoinGeometryUtils.SwitchJoinOrder(document, earlier, later);
                                result.Changed++;
                            }
                        }
                        catch (Exception ex)
                        {
                            result.Skipped++;
                            if (result.Warnings.Count < 8) result.Warnings.Add(ex.Message);
                        }
                    }
                }
                transaction.Commit();
            }

            foreach (FloorElementModel model in components) model.WorkflowStatus = "Đã kiểm tra Join";
            result.Summary = $"Đã kiểm tra {result.Processed} cặp giao nhau; cập nhật {result.Changed} thao tác Join/thứ tự; bỏ qua {result.Skipped}.";
            return result;
        }

        public FormworkWorkflowResult PublishAreas(Document document, IList<FloorElementModel> components,
            Func<string, ComponentFormworkDesign> getDesign, int revision)
        {
            EnsureSelected(document, components);
            EnsureSharedParameters(document);
            var result = new FormworkWorkflowResult();
            using (var transaction = new Transaction(document, "VK - Trích xuất diện tích ván khuôn"))
            {
                transaction.Start();
                foreach (var item in ResolveComponents(document, components))
                {
                    ComponentIntersectionInfo info = item.Model.IntersectionInfo;
                    double bottom = info?.NetBottomAreaM2 ?? (item.Model.ComponentType == StructuralComponentType.Slab ? item.Model.Area : 0);
                    double side = info?.NetSideAreaM2 ?? GetFallbackSideArea(item.Model);
                    double top = info?.NetTopAreaM2 ?? 0;
                    double net = info?.NetPlywoodAreaM2 ?? Math.Max(0, bottom + side + top);
                    ComponentFormworkDesign design = getDesign?.Invoke(item.Model.UniqueId);

                    SetArea(item.Element, NetAreaParameter, net);
                    SetArea(item.Element, BottomAreaParameter, bottom);
                    SetArea(item.Element, SideAreaParameter, side + (info?.InternalOpeningSideAreaM2 ?? 0));
                    SetArea(item.Element, TopAreaParameter, top);
                    SetText(item.Element, IntersectionsParameter,
                        info == null ? "0" : info.IntersectingElementUniqueIds.Count.ToString());
                    SetText(item.Element, ComplianceParameter,
                        design == null ? "Chưa thiết kế" : design.IsCompliant ? "ĐẠT" : "KHÔNG ĐẠT");
                    SetText(item.Element, RevisionParameter, revision <= 0 ? "0" : revision.ToString());
                    item.Model.WorkflowStatus = $"Đã trích xuất {net:F2} m²";
                    result.Processed++;
                    result.Changed++;
                }
                transaction.Commit();
            }
            result.Summary = $"Đã ghi diện tích ván khuôn ròng cho {result.Changed} cấu kiện vào tham số VK_* (phiên bản {revision}).";
            return result;
        }

        public FormworkWorkflowResult CreateSchedules(Document document)
        {
            if (document == null) throw new ArgumentNullException(nameof(document));
            EnsureSharedParameters(document);
            var scheduleSpecs = new[]
            {
                (BuiltInCategory.OST_Floors, "VK - Bóc tách ván khuôn Sàn"),
                (BuiltInCategory.OST_StructuralFraming, "VK - Bóc tách ván khuôn Dầm"),
                (BuiltInCategory.OST_StructuralColumns, "VK - Bóc tách ván khuôn Cột"),
                (BuiltInCategory.OST_StructuralFoundation, "VK - Bóc tách ván khuôn Móng")
            };
            var result = new FormworkWorkflowResult();
            using (var transaction = new Transaction(document, "VK - Tạo bảng thống kê"))
            {
                transaction.Start();
                foreach (var spec in scheduleSpecs)
                {
                    ViewSchedule schedule = new FilteredElementCollector(document)
                        .OfClass(typeof(ViewSchedule)).Cast<ViewSchedule>()
                        .FirstOrDefault(view => !view.IsTemplate && view.Name.Equals(spec.Item2, StringComparison.OrdinalIgnoreCase));
                    if (schedule == null)
                    {
                        schedule = ViewSchedule.CreateSchedule(document, new ElementId(spec.Item1));
                        schedule.Name = spec.Item2;
                        result.Changed++;
                    }
                    ConfigureSchedule(document, schedule);
                    result.Processed++;
                }
                transaction.Commit();
            }
            result.Summary = $"Đã tạo/cập nhật {result.Processed} bảng thống kê Revit cho Sàn, Dầm, Cột và Móng.";
            return result;
        }

        private static void ConfigureSchedule(Document document, ViewSchedule schedule)
        {
            ScheduleDefinition definition = schedule.Definition;
            var wanted = new[]
            {
                SequenceParameter, GroupParameter, TypeParameter, NameParameter,
                NetAreaParameter, BottomAreaParameter, SideAreaParameter, TopAreaParameter,
                IntersectionsParameter, ComplianceParameter, RevisionParameter
            };
            var existing = definition.GetFieldOrder().Select(id => definition.GetField(id))
                .ToDictionary(field => field.GetName(), field => field, StringComparer.OrdinalIgnoreCase);
            foreach (string name in wanted)
            {
                if (existing.ContainsKey(name)) continue;
                SchedulableField schedulable = definition.GetSchedulableFields()
                    .FirstOrDefault(field => field.GetName(document).Equals(name, StringComparison.OrdinalIgnoreCase));
                if (schedulable == null) continue;
                ScheduleField added = definition.AddField(schedulable);
                added.ColumnHeading = GetScheduleHeading(name);
                if (AreaParameters.Contains(name)) added.DisplayType = ScheduleFieldDisplayType.Totals;
            }
            definition.ShowGrandTotal = true;
            definition.ShowGrandTotalTitle = true;
            definition.ShowGrandTotalCount = true;
            definition.GrandTotalTitle = "TỔNG CỘNG";
        }

        private static string GetScheduleHeading(string parameterName)
        {
            switch (parameterName)
            {
                case SequenceParameter: return "Trình tự";
                case GroupParameter: return "Phân khu / Tầng";
                case TypeParameter: return "Loại cấu kiện";
                case NameParameter: return "Tên cấu kiện VK";
                case NetAreaParameter: return "Diện tích VK ròng";
                case BottomAreaParameter: return "Diện tích đáy";
                case SideAreaParameter: return "Diện tích thành";
                case TopAreaParameter: return "Diện tích đỉnh";
                case IntersectionsParameter: return "Số giao cấu kiện";
                case ComplianceParameter: return "Trạng thái thiết kế";
                case RevisionParameter: return "Phiên bản";
                default: return parameterName;
            }
        }

        private static ExternalDefinition GetOrCreateDefinition(DefinitionGroup group, string name, ForgeTypeId dataType)
        {
            ExternalDefinition existing = group.Definitions.get_Item(name) as ExternalDefinition;
            if (existing != null) return existing;
            return group.Definitions.Create(new ExternalDefinitionCreationOptions(name, dataType)
            {
                Description = "Dữ liệu bóc tách ván khuôn do add-in quản lý",
                UserModifiable = true,
                Visible = true
            }) as ExternalDefinition;
        }

        private static bool BindDefinition(Document document, ExternalDefinition definition)
        {
            CategorySet categories = document.Application.Create.NewCategorySet();
            foreach (BuiltInCategory category in new[]
                     {
                         BuiltInCategory.OST_Floors, BuiltInCategory.OST_StructuralFraming,
                         BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_StructuralFoundation
                     })
            {
                Category value = Category.GetCategory(document, category);
                if (value != null && value.AllowsBoundParameters) categories.Insert(value);
            }
            InstanceBinding binding = document.Application.Create.NewInstanceBinding(categories);
            BindingMap map = document.ParameterBindings;
            return map.get_Item(definition) == null
                ? map.Insert(definition, binding, GroupTypeId.Data)
                : map.ReInsert(definition, binding, GroupTypeId.Data);
        }

        private static void EnsureSharedParameterFile(string path)
        {
            if (File.Exists(path) && new FileInfo(path).Length > 0) return;
            File.WriteAllText(path,
                "# This is a Revit shared parameter file.\r\n" +
                "# Do not edit manually.\r\n" +
                "*META\tVERSION\tMINVERSION\r\nMETA\t2\t1\r\n" +
                "*GROUP\tID\tNAME\r\n" +
                "*PARAM\tGUID\tNAME\tDATATYPE\tDATACATEGORY\tGROUP\tVISIBLE\tDESCRIPTION\tUSERMODIFIABLE\r\n");
        }

        private static IEnumerable<(FloorElementModel Model, Element Element)> ResolveComponents(Document document,
            IEnumerable<FloorElementModel> components)
        {
            foreach (FloorElementModel model in components ?? Enumerable.Empty<FloorElementModel>())
            {
                Element element = !string.IsNullOrWhiteSpace(model.UniqueId) ? document.GetElement(model.UniqueId) : null;
                if (element == null && long.TryParse(model.ElementId, out long id)) element = document.GetElement(new ElementId(id));
                if (element != null) yield return (model, element);
            }
        }

        private static void EnsureSelected(Document document, IList<FloorElementModel> components)
        {
            if (document == null) throw new InvalidOperationException("Không có tài liệu Revit đang hoạt động.");
            if (components == null || components.Count == 0)
                throw new InvalidOperationException("Chưa chọn cấu kiện để thực hiện quy trình bóc tách ván khuôn.");
        }

        private static bool BoundsOverlap(BoundingBoxXYZ a, BoundingBoxXYZ b)
        {
            if (a == null || b == null) return false;
            const double tolerance = 0.01;
            return a.Min.X <= b.Max.X + tolerance && a.Max.X + tolerance >= b.Min.X &&
                   a.Min.Y <= b.Max.Y + tolerance && a.Max.Y + tolerance >= b.Min.Y &&
                   a.Min.Z <= b.Max.Z + tolerance && a.Max.Z + tolerance >= b.Min.Z;
        }

        private static double GetLevelElevation(Document document, Element element)
        {
            Level level = document.GetElement(element.LevelId) as Level;
            return level?.Elevation ?? element.get_BoundingBox(null)?.Min.Z ?? 0;
        }

        private static int GetPriority(StructuralComponentType type)
        {
            switch (type)
            {
                case StructuralComponentType.Foundation: return 1;
                case StructuralComponentType.Column: return 2;
                case StructuralComponentType.Beam: return 3;
                default: return 4;
            }
        }

        private static string GetComponentCode(StructuralComponentType type)
        {
            switch (type)
            {
                case StructuralComponentType.Foundation: return "M";
                case StructuralComponentType.Column: return "C";
                case StructuralComponentType.Beam: return "D";
                default: return "S";
            }
        }

        private static string GetComponentLabel(StructuralComponentType type)
        {
            switch (type)
            {
                case StructuralComponentType.Foundation: return "Móng";
                case StructuralComponentType.Column: return "Cột";
                case StructuralComponentType.Beam: return "Dầm";
                default: return "Sàn";
            }
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "NA";
            char[] chars = value.ToUpperInvariant().Select(ch => char.IsLetterOrDigit(ch) ? ch : '-').ToArray();
            return new string(chars).Trim('-');
        }

        private static double GetFallbackSideArea(FloorElementModel model)
        {
            if (model.ComponentType == StructuralComponentType.Column)
                return 2 * (model.Elevation + model.Thickness) * model.StoryHeight / 1_000_000.0;
            if (model.ComponentType == StructuralComponentType.Beam) return Math.Max(0, model.Area);
            if (model.ComponentType == StructuralComponentType.Foundation)
                return Math.Max(0, model.Perimeter * model.Thickness / 1000.0);
            return Math.Max(0, model.Perimeter * model.Thickness / 1000.0);
        }

        private static void SetText(Element element, string parameterName, string value)
        {
            Parameter parameter = element.LookupParameter(parameterName);
            if (parameter != null && !parameter.IsReadOnly) parameter.Set(value ?? string.Empty);
        }

        private static void SetArea(Element element, string parameterName, double squareMeters)
        {
            Parameter parameter = element.LookupParameter(parameterName);
            if (parameter != null && !parameter.IsReadOnly)
                parameter.Set(UnitUtils.ConvertToInternalUnits(Math.Max(0, squareMeters), UnitTypeId.SquareMeters));
        }
    }
}
