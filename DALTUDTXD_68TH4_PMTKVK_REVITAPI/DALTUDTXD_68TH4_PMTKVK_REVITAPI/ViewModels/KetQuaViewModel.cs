using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;
using System.Linq;
using System.Windows;
using ClosedXML.Excel;
using MiniSoftware;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class KetQuaViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly SharedDataViewModel _SharedDataViewModel;
        private readonly IDialogService _dialogService;
        private string _revitVersionStatus;
        private List<MaterialItem> _materialQuantities;
        private string _selectedComponentQuantitiesTitle = "BẢNG TIÊN LƯỢNG VẬT TƯ / PHỤ KIỆN (CẤU KIỆN ĐANG CHỌN)";
        private bool _isWholeProjectTakeoff;
        
        public event PropertyChangedEventHandler PropertyChanged;

        public KetQuaViewModel(SharedDataViewModel SharedDataViewModel, IDialogService dialogService = null)
        {
            _SharedDataViewModel = SharedDataViewModel ?? throw new ArgumentNullException(nameof(SharedDataViewModel));
            _dialogService = dialogService ?? new MessageBoxDialogService();

            _SharedDataViewModel.PropertyChanged += OnSharedDataViewModelPropertyChanged;
            _SharedDataViewModel.SlabSelectionCompleted += OnSlabSelectionCompleted;
            
            _revitVersionStatus = "Revit API - Báo cáo kết quả thiết kế";

            UpdateMaterialQuantities();
            _SharedDataViewModel.SynchronizeScheduleFromValidatedDesign();

            CreateRevitModelCommand = new RelayCommand(o => ExecuteCreateRevitModel());
            CalculateAllFloorsCommand = new RelayCommand(o => ExecuteCalculateAllFloors());
            ExportExcelCommand = new RelayCommand(o => ExecuteExportExcel());
            ExportPdfCommand = new RelayCommand(o => ExecuteExportPdf());
            ExportWordCommand = new RelayCommand(o => ExecuteExportWord());
            ChangeTabCommand = new RelayCommand(ExecuteChangeTab);
            ShowFloorDetailsCommand = new RelayCommand(ExecuteShowFloorDetails);
            SelectFloorCommand = new RelayCommand(o => _SharedDataViewModel.ExecuteSelectComponentFromKetQua());
            SweepSelectFloorCommand = new RelayCommand(o => _SharedDataViewModel.ExecuteSweepSelectComponentFromKetQua());

            if (_SharedDataViewModel.LatestSelectionContext != null)
            {
                OnSlabSelectionCompleted(this, _SharedDataViewModel.LatestSelectionContext);
            }
        }

        public double Joist1Spacing { get => _SharedDataViewModel.Joist1Spacing; set { _SharedDataViewModel.Joist1Spacing = value; } }
        public double Joist2Spacing { get => _SharedDataViewModel.Joist2Spacing; set { _SharedDataViewModel.Joist2Spacing = value; } }
        public double SupportSpacing { get => _SharedDataViewModel.SupportSpacing; set { _SharedDataViewModel.SupportSpacing = value; } }
        public string RevitVersionStatus { get => _revitVersionStatus; set { _revitVersionStatus = value; OnPropertyChanged(); } }
        public List<MaterialItem> MaterialQuantities { get => _materialQuantities; set { _materialQuantities = value; OnPropertyChanged(); } }
        public List<MaterialItem> ProjectMaterialQuantities { get => _SharedDataViewModel.KetQuaProjectMaterialQuantities as List<MaterialItem>; set { _SharedDataViewModel.KetQuaProjectMaterialQuantities = value; OnPropertyChanged(); } }
        public List<FloorElementModel> FloorElements { get => _SharedDataViewModel.KetQuaFloorElements as List<FloorElementModel>; set { _SharedDataViewModel.KetQuaFloorElements = value; OnPropertyChanged(); } }
        public FrameworkElement ProjectPreview { get => _SharedDataViewModel.KetQuaPreviewControl; }

        public string SelectedComponentQuantitiesTitle { get => _selectedComponentQuantitiesTitle; set { _selectedComponentQuantitiesTitle = value; OnPropertyChanged(); } }
        public string ProjectComponentListTitle => "DANH SÁCH CÁC CẤU KIỆN TRONG DỰ ÁN";
        public bool IsFilterSlabChecked { get => _SharedDataViewModel.IsFilterSlabChecked; set { _SharedDataViewModel.IsFilterSlabChecked = value; OnPropertyChanged(); } }
        public bool IsFilterFoundationChecked { get => _SharedDataViewModel.IsFilterFoundationChecked; set { _SharedDataViewModel.IsFilterFoundationChecked = value; OnPropertyChanged(); } }
        public bool IsFilterBeamChecked { get => _SharedDataViewModel.IsFilterBeamChecked; set { _SharedDataViewModel.IsFilterBeamChecked = value; OnPropertyChanged(); } }
        public bool IsFilterColumnChecked { get => _SharedDataViewModel.IsFilterColumnChecked; set { _SharedDataViewModel.IsFilterColumnChecked = value; OnPropertyChanged(); } }

        public string Param1Label => _SharedDataViewModel.CurrentComponentType == StructuralComponentType.Beam ? "Khoảng cách đà đáy:" :
                                    (_SharedDataViewModel.CurrentComponentType == StructuralComponentType.Column ? "Khoảng cách gông:" : "Khoảng cách XG phụ:");
        public string Param1Value => _SharedDataViewModel.CurrentComponentType == StructuralComponentType.Beam ? _SharedDataViewModel.BeamBottomYokeSpacing.ToString("F0") :
                                    (_SharedDataViewModel.CurrentComponentType == StructuralComponentType.Column ? _SharedDataViewModel.ColumnYokeSpacing.ToString("F0") : _SharedDataViewModel.Joist1Spacing.ToString("F0"));
        
        public string Param2Label => _SharedDataViewModel.CurrentComponentType == StructuralComponentType.Beam ? "Khoảng cách gông thành:" :
                                    (_SharedDataViewModel.CurrentComponentType == StructuralComponentType.Column ? "" : "Khoảng cách XG chính:");
        public string Param2Value => _SharedDataViewModel.CurrentComponentType == StructuralComponentType.Beam ? _SharedDataViewModel.BeamSideYokeSpacing.ToString("F0") :
                                    (_SharedDataViewModel.CurrentComponentType == StructuralComponentType.Column ? "" : _SharedDataViewModel.Joist2Spacing.ToString("F0"));
        
        public string Param3Label => _SharedDataViewModel.CurrentComponentType == StructuralComponentType.Beam ? "Khoảng cách chống dầm:" :
                                    (_SharedDataViewModel.CurrentComponentType == StructuralComponentType.Column ? "" : "Khoảng cách chống sàn:");
        public string Param3Value => _SharedDataViewModel.CurrentComponentType == StructuralComponentType.Beam ? _SharedDataViewModel.BeamPropSpacing.ToString("F0") :
                                    (_SharedDataViewModel.CurrentComponentType == StructuralComponentType.Column ? "" : _SharedDataViewModel.SupportSpacing.ToString("F0"));

        public ICommand CreateRevitModelCommand { get; }
        public ICommand CalculateAllFloorsCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand ExportWordCommand { get; }
        public ICommand ChangeTabCommand { get; }
        public ICommand ShowFloorDetailsCommand { get; }
        public ICommand SelectFloorCommand { get; }
        public ICommand SweepSelectFloorCommand { get; }

        public void Dispose()
        {
            _SharedDataViewModel.PropertyChanged -= OnSharedDataViewModelPropertyChanged;
            _SharedDataViewModel.SlabSelectionCompleted -= OnSlabSelectionCompleted;
        }


        private void ExecuteCreateRevitModel()
        {
            _dialogService.ShowInfo("Đang khởi tạo dữ liệu mô hình 3D từ các thông số tính toán.\n\n" +
                            $"- Lưới xà gồ phụ: {Joist1Spacing:F0} mm\n" +
                            $"- Lưới xà gồ chính: {Joist2Spacing:F0} mm\n" +
                            $"- Khoảng cách giáo chống: {SupportSpacing:F0} mm\n\n" +
                            "Hệ thống sẽ chuyển sang trang Xem mô hình 3D để bắt đầu quá trình render.", "Khởi tạo 3D");
            
            RevitVersionStatus = "Bắt đầu quá trình render 3D lúc " + DateTime.Now.ToString("HH:mm:ss");
            
            // Kích hoạt pipeline render 3D cross-page
            _SharedDataViewModel.RequestRender3D();
        }

        private void ExecuteCalculateAllFloors()
        {
            ExecuteCalculateWholeProject();
        }

        private void ExecuteCalculateWholeProject()
        {
            Document document = _SharedDataViewModel.RevitDoc;
            if (document == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            try
            {
                bool includeSlabs = IsFilterSlabChecked;
                bool includeFoundations = IsFilterFoundationChecked;
                bool includeBeams = IsFilterBeamChecked;
                bool includeColumns = IsFilterColumnChecked;
                if (!includeSlabs && !includeFoundations && !includeBeams && !includeColumns)
                    includeSlabs = includeFoundations = includeBeams = includeColumns = true;

                var components = new List<FloorElementModel>();
                var takeoff = new Dictionary<string, MaterialItem>();
                int slabCount = 0, foundationCount = 0, beamCount = 0, columnCount = 0;
                double slabArea = 0, foundationArea = 0, beamLength = 0, columnHeight = 0;

                void AddItems(IEnumerable<MaterialItem> items)
                {
                    foreach (MaterialItem item in items)
                    {
                        string key = $"{item.MaterialName}|{item.Specification}|{item.Unit}";
                        if (takeoff.TryGetValue(key, out MaterialItem existing)) existing.Quantity += item.Quantity;
                        else takeoff[key] = new MaterialItem
                        {
                            MaterialName = item.MaterialName,
                            Specification = item.Specification,
                            Quantity = item.Quantity,
                            Unit = item.Unit
                        };
                    }
                }

                string LevelName(Element element)
                {
                    if (element.LevelId != ElementId.InvalidElementId && document.GetElement(element.LevelId) is Level level)
                        return level.Name;
                    return "Không xác định";
                }

                double DimensionMm(Element element, string[] names, double fallback)
                {
                    foreach (string name in names)
                    {
                        Parameter parameter = element.LookupParameter(name);
                        if (parameter == null && element is FamilyInstance instance)
                            parameter = instance.Symbol?.LookupParameter(name);
                        if (parameter == null && document.GetElement(element.GetTypeId()) is ElementType elementType)
                            parameter = elementType.LookupParameter(name);
                        if (parameter != null && parameter.StorageType == StorageType.Double && parameter.AsDouble() > 0)
                            return parameter.AsDouble() * 304.8;
                    }
                    return fallback;
                }

                if (includeSlabs || includeFoundations)
                {
                    var floorCategories = new[] { BuiltInCategory.OST_Floors, BuiltInCategory.OST_StructuralFoundation };
                    ICollection<Element> floorElements = new FilteredElementCollector(document)
                        .WherePasses(new ElementMulticategoryFilter(floorCategories))
                        .WhereElementIsNotElementType().ToElements();
                    foreach (Element element in floorElements)
                    {
                        bool foundation = element.Category != null &&
                                          (BuiltInCategory)element.Category.Id.Value == BuiltInCategory.OST_StructuralFoundation;
                        if (!foundation && element is Floor floor && document.GetElement(floor.GetTypeId()) is FloorType floorType)
                            foundation = floorType.IsFoundationSlab;
                        if (foundation && !includeFoundations || !foundation && !includeSlabs) continue;

                        double area = (element.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED)?.AsDouble() ?? 0) * 0.092903;
                        double perimeter = (element.get_Parameter(BuiltInParameter.HOST_PERIMETER_COMPUTED)?.AsDouble() ?? 0) * 0.3048;
                        Parameter thicknessParameter = element.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM) ??
                                                       (document.GetElement(element.GetTypeId()) as ElementType)?.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
                        double thickness = thicknessParameter != null && thicknessParameter.AsDouble() > 0
                            ? thicknessParameter.AsDouble() * 304.8
                            : DimensionMm(element, new[] { "Thickness", "Chiều dày", "h", "H" },
                                foundation ? 500 : Math.Max(100, _SharedDataViewModel.SlabThickness));
                        BoundingBoxXYZ box = element.get_BoundingBox(null);
                        double lengthMm = box == null ? Math.Sqrt(Math.Max(1, area)) * 1000 : (box.Max.X - box.Min.X) * 304.8;
                        double widthMm = box == null ? Math.Sqrt(Math.Max(1, area)) * 1000 : (box.Max.Y - box.Min.Y) * 304.8;

                        if (foundation)
                        {
                            foundationCount++;
                            foundationArea += area;
                            AddItems(CalculateFoundationQuantities(lengthMm, widthMm, thickness, perimeter,
                                _SharedDataViewModel.GetComponentFormworkDesign(element.UniqueId)?.Parameters));
                        }
                        else
                        {
                            slabCount++;
                            slabArea += area;
                            AddItems(CalculateQuantities(area, perimeter, thickness,
                                _SharedDataViewModel.GetComponentFormworkDesign(element.UniqueId)?.Parameters));
                        }
                        components.Add(new FloorElementModel
                        {
                            UniqueId = element.UniqueId,
                            ElementId = element.Id.ToString(),
                            ComponentType = foundation ? StructuralComponentType.Foundation : StructuralComponentType.Slab,
                            FamilyTypeName = (foundation ? "MÓNG · " : "SÀN · ") + element.Name,
                            Level = LevelName(element),
                            Area = area,
                            Perimeter = perimeter,
                            Thickness = thickness,
                            StoryHeight = foundation ? lengthMm : 0,
                            Elevation = foundation ? widthMm : 0,
                            ComponentSummary = foundation
                                ? $"Diện tích: {area:F2} m² · Cao/dày: {thickness:F0} mm"
                                : $"Diện tích: {area:F2} m² · Dày: {thickness:F0} mm"
                        });
                    }
                }

                if (includeBeams)
                {
                    foreach (Element element in new FilteredElementCollector(document)
                                 .OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsNotElementType())
                    {
                        double lengthM = (element.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH)?.AsDouble() ?? 0) * 0.3048;
                        if (lengthM <= 0 && element.Location is LocationCurve curve) lengthM = curve.Curve.Length * 0.3048;
                        if (lengthM <= 0) continue;
                        BoundingBoxXYZ box = element.get_BoundingBox(null);
                        double fallbackWidth = box == null ? 200 : Math.Min(box.Max.X - box.Min.X, box.Max.Y - box.Min.Y) * 304.8;
                        double fallbackDepth = box == null ? 400 : (box.Max.Z - box.Min.Z) * 304.8;
                        double width = DimensionMm(element, new[] { "b", "Width", "B", "Chiều rộng" }, fallbackWidth);
                        double depth = DimensionMm(element, new[] { "h", "Depth", "H", "Chiều cao" }, fallbackDepth);
                        beamCount++;
                        beamLength += lengthM;
                        AddItems(CalculateBeamQuantities(width, depth, lengthM * 1000,
                            _SharedDataViewModel.GetComponentFormworkDesign(element.UniqueId)?.Parameters));
                        components.Add(new FloorElementModel
                        {
                            UniqueId = element.UniqueId,
                            ElementId = element.Id.ToString(),
                            ComponentType = StructuralComponentType.Beam,
                            FamilyTypeName = "DẦM · " + element.Name,
                            Level = LevelName(element),
                            Area = lengthM * (width + 2 * depth) / 1000.0,
                            Perimeter = lengthM,
                            Thickness = depth,
                            StoryHeight = lengthM * 1000.0,
                            Elevation = width,
                            ComponentSummary = $"Dài: {lengthM:F2} m · Tiết diện: {width:F0}×{depth:F0} mm"
                        });
                    }
                }

                if (includeColumns)
                {
                    var columnCategories = new[] { BuiltInCategory.OST_StructuralColumns, BuiltInCategory.OST_Columns };
                    foreach (Element element in new FilteredElementCollector(document)
                                 .WherePasses(new ElementMulticategoryFilter(columnCategories)).WhereElementIsNotElementType())
                    {
                        BoundingBoxXYZ box = element.get_BoundingBox(null);
                        if (box == null) continue;
                        double heightM = (box.Max.Z - box.Min.Z) * 0.3048;
                        double fallbackWidth = (box.Max.X - box.Min.X) * 304.8;
                        double fallbackDepth = (box.Max.Y - box.Min.Y) * 304.8;
                        double width = DimensionMm(element, new[] { "b", "Width", "B", "Chiều rộng" }, fallbackWidth);
                        double depth = DimensionMm(element, new[] { "h", "Depth", "H", "Chiều sâu" }, fallbackDepth);
                        columnCount++;
                        columnHeight += heightM;
                        AddItems(CalculateColumnQuantities(width, depth, heightM * 1000,
                            _SharedDataViewModel.GetComponentFormworkDesign(element.UniqueId)?.Parameters));
                        components.Add(new FloorElementModel
                        {
                            UniqueId = element.UniqueId,
                            ElementId = element.Id.ToString(),
                            ComponentType = StructuralComponentType.Column,
                            FamilyTypeName = "CỘT · " + element.Name,
                            Level = LevelName(element),
                            Area = 2 * (width + depth) / 1000.0 * heightM,
                            Perimeter = 2 * (width + depth) / 1000.0,
                            Thickness = depth,
                            StoryHeight = heightM * 1000.0,
                            Elevation = width,
                            ComponentSummary = $"Cao: {heightM:F2} m · Tiết diện: {width:F0}×{depth:F0} mm"
                        });
                    }
                }

                // Build intersections only after all four structural categories exist.
                // The preliminary isolated takeoff above is intentionally discarded.
                _SharedDataViewModel.FloorElements = components;
                FloorElements = components;
                ProjectMaterialQuantities = AggregateComponentTakeoff(components);
                MaterialQuantities = ProjectMaterialQuantities;
                _isWholeProjectTakeoff = true;
                SelectedComponentQuantitiesTitle = "BẢNG TIÊN LƯỢNG VẬT TƯ / HỆ CHỐNG / PHỤ KIỆN TOÀN DỰ ÁN";
                _SharedDataViewModel.KetQuaTotalProjectArea = slabArea + foundationArea;
                _SharedDataViewModel.KetQuaTotalProjectPerimeter = beamLength + columnHeight;
                _SharedDataViewModel.KetQuaIsProjectCalculated = true;

                string message = "Đã thống kê toàn bộ phạm vi dự án:\n" +
                                 $"• Sàn: {slabCount} cấu kiện · {slabArea:F2} m²\n" +
                                 $"• Móng: {foundationCount} cấu kiện · {foundationArea:F2} m²\n" +
                                 $"• Dầm: {beamCount} cấu kiện · {beamLength:F2} m\n" +
                                 $"• Cột: {columnCount} cấu kiện · {columnHeight:F2} m\n\n" +
                                 $"Bảng tiên lượng đã cập nhật {ProjectMaterialQuantities.Count} dòng vật tư, hệ chống và phụ kiện.";
                _dialogService.ShowInfo(message, "Thống kê toàn dự án");
                RevitVersionStatus = $"Toàn dự án: {slabCount} sàn · {foundationCount} móng · {beamCount} dầm · {columnCount} cột · {ProjectMaterialQuantities.Count} dòng vật tư/phụ kiện";
            }
            catch (Exception ex)
            {
                _dialogService.ShowWarning($"Có lỗi xảy ra trong quá trình thống kê toàn dự án: {ex.Message}", "Lỗi");
            }
        }

        private void ExecuteCalculateAllSlabs()
        {
            if (_SharedDataViewModel.RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            try
            {
                bool isFoundation = _SharedDataViewModel.CurrentComponentType == StructuralComponentType.Foundation;
                string compNameLower = isFoundation ? "móng" : "sàn";

                var slabCats = new List<BuiltInCategory> { BuiltInCategory.OST_Floors, BuiltInCategory.OST_StructuralFoundation };
                ElementMulticategoryFilter slabFilter = new ElementMulticategoryFilter(slabCats);
                FilteredElementCollector collector = new FilteredElementCollector(_SharedDataViewModel.RevitDoc);
                ICollection<Element> floors = collector.WherePasses(slabFilter).WhereElementIsNotElementType().ToElements();

                double totalAreaSqFt = 0;
                double totalPerimeterFt = 0;
                int floorCount = 0;
                
                var floorList = new List<FloorElementModel>();

                foreach (Element elem in floors)
                {
                    BuiltInCategory cat = (BuiltInCategory)elem.Category.Id.Value;
                    bool elemIsFoundation = false;

                    if (cat == BuiltInCategory.OST_StructuralFoundation)
                    {
                        elemIsFoundation = true;
                    }
                    else if (cat == BuiltInCategory.OST_Floors && elem is Floor floor)
                    {
                        if (floor.Document.GetElement(floor.GetTypeId()) as FloorType is FloorType floorType && floorType.IsFoundationSlab)
                        {
                            elemIsFoundation = true;
                        }
                    }

                    if (isFoundation && !elemIsFoundation) continue;
                    if (!isFoundation && elemIsFoundation) continue;

                    double areaM2 = 0;
                    double perimeterM = 0;
                    double thicknessMm = 0;
                    
                    Parameter areaParam = elem.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                    if (areaParam != null)
                    {
                        totalAreaSqFt += areaParam.AsDouble();
                        areaM2 = areaParam.AsDouble() * 0.092903;
                    }
                    
                    Parameter perimParam = elem.get_Parameter(BuiltInParameter.HOST_PERIMETER_COMPUTED);
                    if (perimParam != null)
                    {
                        totalPerimeterFt += perimParam.AsDouble();
                        perimeterM = perimParam.AsDouble() * 0.3048;
                    }
                    
                    Parameter thicknessParam = elem.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM) ?? elem.LookupParameter("Thickness") ?? elem.LookupParameter("Chiều dày");
                    if (thicknessParam != null)
                    {
                        thicknessMm = thicknessParam.AsDouble() * 304.8;
                    }
                    else
                    {
                        ElementType elemType = elem.Document.GetElement(elem.GetTypeId()) as ElementType;
                        if (elemType != null)
                        {
                            Parameter typeThicknessParam = elemType.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM) ?? elemType.LookupParameter("Thickness") ?? elemType.LookupParameter("Chiều dày");
                            if (typeThicknessParam != null) thicknessMm = typeThicknessParam.AsDouble() * 304.8;
                        }
                    }
                    if (thicknessMm <= 0) thicknessMm = 150.0;

                    string levelName = "Không xác định";
                    if (elem.LevelId != ElementId.InvalidElementId)
                    {
                        Level level = elem.Document.GetElement(elem.LevelId) as Level;
                        if (level != null) levelName = level.Name;
                    }
                    
                    floorList.Add(new FloorElementModel
                    {
                        UniqueId = elem.UniqueId,
                        ElementId = elem.Id.ToString(),
                        FamilyTypeName = elem.Name,
                        Level = levelName,
                        Area = areaM2,
                        Perimeter = perimeterM,
                        Thickness = thicknessMm
                    });
                    
                    floorCount++;
                }

                FloorElements = floorList;

                if (floorCount > 0)
                {
                    _SharedDataViewModel.KetQuaTotalProjectArea = totalAreaSqFt * 0.092903;
                    _SharedDataViewModel.KetQuaTotalProjectPerimeter = totalPerimeterFt * 0.3048;
                    _SharedDataViewModel.KetQuaIsProjectCalculated = true;
                    UpdateProjectMaterialQuantities();
                    
                    _dialogService.ShowInfo(
                        $"Đã tổng hợp thành công diện tích của {floorCount} {compNameLower} trong dự án.\n" +
                        $"Tổng diện tích: {_SharedDataViewModel.KetQuaTotalProjectArea:F2} m²\n\n" +
                        $"Bảng tiên lượng vật tư toàn dự án đã được cập nhật tự động bên dưới.",
                        "Thống kê toàn dự án");
                        
                    RevitVersionStatus = $"Đã thống kê {floorCount} {compNameLower} - Tổng diện tích: {_SharedDataViewModel.KetQuaTotalProjectArea:F2} m²";
                }
                else
                {
                    _dialogService.ShowWarning($"Không tìm thấy cấu kiện {compNameLower} nào trong dự án.", "Thông báo");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowWarning($"Có lỗi xảy ra trong quá trình thống kê: {ex.Message}", "Lỗi");
            }
        }

        private void ExecuteCalculateAllBeams()
        {
            if (_SharedDataViewModel.RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            try
            {
                FilteredElementCollector collector = new FilteredElementCollector(_SharedDataViewModel.RevitDoc);
                ICollection<Element> beams = collector.OfCategory(BuiltInCategory.OST_StructuralFraming).WhereElementIsNotElementType().ToElements();

                double totalLengthFt = 0;
                int beamCount = 0;
                
                var beamList = new List<FloorElementModel>(); // Reusing FloorElementModel for now to bind to the same UI list

                foreach (Element elem in beams)
                {
                    if (elem is FamilyInstance beam)
                    {
                        double lengthM = 0;
                        double b = 200, h = 400; // default
                        
                        Parameter lengthParam = beam.get_Parameter(BuiltInParameter.STRUCTURAL_FRAME_CUT_LENGTH);
                        if (lengthParam != null)
                        {
                            totalLengthFt += lengthParam.AsDouble();
                            lengthM = lengthParam.AsDouble() * 0.3048;
                        }

                        Parameter bParam = beam.Symbol.LookupParameter("b") ?? beam.Symbol.LookupParameter("Width") ?? beam.Symbol.LookupParameter("B");
                        Parameter hParam = beam.Symbol.LookupParameter("h") ?? beam.Symbol.LookupParameter("Depth") ?? beam.Symbol.LookupParameter("H");
                        
                        if (bParam != null) b = bParam.AsDouble() * 304.8;
                        if (hParam != null) h = hParam.AsDouble() * 304.8;

                        string levelName = "Không xác định";
                        if (beam.LevelId != ElementId.InvalidElementId)
                        {
                            Level level = beam.Document.GetElement(beam.LevelId) as Level;
                            if (level != null) levelName = level.Name;
                        }
                        
                        beamList.Add(new FloorElementModel
                        {
                            UniqueId = beam.UniqueId,
                            ElementId = beam.Id.ToString(),
                            FamilyTypeName = beam.Name,
                            Level = levelName,
                            Area = lengthM * b / 1000.0, // Mock area
                            Perimeter = lengthM,         // Store length here
                            Thickness = h
                        });
                        
                        beamCount++;
                    }
                }

                FloorElements = beamList;

                if (beamCount > 0)
                {
                    _SharedDataViewModel.KetQuaTotalProjectArea = totalLengthFt * 0.3048; // Store Total Length in Area property for calculation reuse
                    _SharedDataViewModel.KetQuaIsProjectCalculated = true;
                    UpdateProjectMaterialQuantities();
                    
                    _dialogService.ShowInfo(
                        $"Đã tổng hợp thành công {beamCount} dầm trong dự án.\n" +
                        $"Tổng chiều dài: {_SharedDataViewModel.KetQuaTotalProjectArea:F2} m\n\n" +
                        $"Bảng tiên lượng vật tư toàn dự án đã được cập nhật tự động bên dưới.",
                        "Thống kê toàn dự án");
                        
                    RevitVersionStatus = $"Đã thống kê {beamCount} dầm - Tổng chiều dài: {_SharedDataViewModel.KetQuaTotalProjectArea:F2} m";
                }
                else
                {
                    _dialogService.ShowWarning("Không tìm thấy cấu kiện dầm nào trong dự án.", "Thông báo");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowWarning($"Có lỗi xảy ra trong quá trình thống kê dầm: {ex.Message}", "Lỗi");
            }
        }

        private void ExecuteCalculateAllColumns()
        {
            if (_SharedDataViewModel.RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            try
            {
                FilteredElementCollector collector = new FilteredElementCollector(_SharedDataViewModel.RevitDoc);
                ICollection<Element> columns = collector.OfCategory(BuiltInCategory.OST_StructuralColumns).WhereElementIsNotElementType().ToElements();

                double totalHeightFt = 0;
                int columnCount = 0;
                
                var columnList = new List<FloorElementModel>(); // Reusing FloorElementModel for UI

                foreach (Element elem in columns)
                {
                    if (elem is FamilyInstance col)
                    {
                        double heightM = 0;
                        double b = 400, h = 400; // default
                        
                        BoundingBoxXYZ bbox = col.get_BoundingBox(null);
                        if (bbox != null)
                        {
                            double height = bbox.Max.Z - bbox.Min.Z;
                            totalHeightFt += height;
                            heightM = height * 0.3048;
                        }

                        Parameter bParam = col.Symbol.LookupParameter("b") ?? col.Symbol.LookupParameter("Width") ?? col.Symbol.LookupParameter("B");
                        Parameter hParam = col.Symbol.LookupParameter("h") ?? col.Symbol.LookupParameter("Depth") ?? col.Symbol.LookupParameter("H");
                        
                        if (bParam != null) b = bParam.AsDouble() * 304.8;
                        if (hParam != null) h = hParam.AsDouble() * 304.8;

                        string levelName = "Không xác định";
                        if (col.LevelId != ElementId.InvalidElementId)
                        {
                            Level level = col.Document.GetElement(col.LevelId) as Level;
                            if (level != null) levelName = level.Name;
                        }
                        
                        columnList.Add(new FloorElementModel
                        {
                            UniqueId = col.UniqueId,
                            ElementId = col.Id.ToString(),
                            FamilyTypeName = col.Name,
                            Level = levelName,
                            Area = heightM, // Store height
                            Perimeter = (b + h) * 2 / 1000.0, // Store perimeter
                            Thickness = h
                        });
                        
                        columnCount++;
                    }
                }

                FloorElements = columnList;

                if (columnCount > 0)
                {
                    _SharedDataViewModel.KetQuaTotalProjectArea = totalHeightFt * 0.3048; // Store Total Height
                    _SharedDataViewModel.KetQuaTotalProjectPerimeter = columnCount; // Store Count
                    _SharedDataViewModel.KetQuaIsProjectCalculated = true;
                    UpdateProjectMaterialQuantities();
                    
                    _dialogService.ShowInfo(
                        $"Đã tổng hợp thành công {columnCount} cột trong dự án.\n" +
                        $"Tổng chiều cao: {_SharedDataViewModel.KetQuaTotalProjectArea:F2} m\n\n" +
                        $"Bảng tiên lượng vật tư toàn dự án đã được cập nhật tự động bên dưới.",
                        "Thống kê toàn dự án");
                        
                    RevitVersionStatus = $"Đã thống kê {columnCount} cột - Tổng chiều cao: {_SharedDataViewModel.KetQuaTotalProjectArea:F2} m";
                }
                else
                {
                    _dialogService.ShowWarning("Không tìm thấy cấu kiện cột nào trong dự án.", "Thông báo");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowWarning($"Có lỗi xảy ra trong quá trình thống kê cột: {ex.Message}", "Lỗi");
            }
        }

        private void ExecuteExportExcel()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "Excel Files (*.xlsx)|*.xlsx",
                    DefaultExt = ".xlsx",
                    FileName = "BangTienLuongVatTu.xlsx"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    using (var workbook = new XLWorkbook())
                    {
                        if (MaterialQuantities != null && MaterialQuantities.Count > 0)
                        {
                            var sheetName = _SharedDataViewModel.CurrentComponentType == StructuralComponentType.Beam ? "Dầm Đang Chọn" :
                                            (_SharedDataViewModel.CurrentComponentType == StructuralComponentType.Column ? "Cột Đang Chọn" : "Sàn Đang Chọn");
                            var ws = workbook.Worksheets.Add(sheetName);
                            ws.Cell(1, 1).Value = "Tên vật tư";
                            ws.Cell(1, 2).Value = "Quy cách";
                            ws.Cell(1, 3).Value = "Khối lượng";
                            ws.Cell(1, 4).Value = "Đơn vị";
                            ws.Range("A1:D1").Style.Font.Bold = true;

                            for (int i = 0; i < MaterialQuantities.Count; i++)
                            {
                                ws.Cell(i + 2, 1).Value = MaterialQuantities[i].MaterialName;
                                ws.Cell(i + 2, 2).Value = MaterialQuantities[i].Specification;
                                ws.Cell(i + 2, 3).Value = MaterialQuantities[i].Quantity;
                                ws.Cell(i + 2, 4).Value = MaterialQuantities[i].Unit;
                            }
                            ws.Columns().AdjustToContents();
                        }

                        if (ProjectMaterialQuantities != null && ProjectMaterialQuantities.Count > 0)
                        {
                            var wsProj = workbook.Worksheets.Add("Toàn Dự Án");
                            wsProj.Cell(1, 1).Value = "Tên vật tư";
                            wsProj.Cell(1, 2).Value = "Quy cách";
                            wsProj.Cell(1, 3).Value = "Khối lượng";
                            wsProj.Cell(1, 4).Value = "Đơn vị";
                            wsProj.Range("A1:D1").Style.Font.Bold = true;

                            for (int i = 0; i < ProjectMaterialQuantities.Count; i++)
                            {
                                wsProj.Cell(i + 2, 1).Value = ProjectMaterialQuantities[i].MaterialName;
                                wsProj.Cell(i + 2, 2).Value = ProjectMaterialQuantities[i].Specification;
                                wsProj.Cell(i + 2, 3).Value = ProjectMaterialQuantities[i].Quantity;
                                wsProj.Cell(i + 2, 4).Value = ProjectMaterialQuantities[i].Unit;
                            }
                            wsProj.Columns().AdjustToContents();
                        }

                        if (workbook.Worksheets.Count == 0)
                        {
                            workbook.Worksheets.Add("Trống");
                        }

                        workbook.SaveAs(saveFileDialog.FileName);
                    }

                    _dialogService.ShowInfo($"Xuất tiên lượng và bảng tính vật liệu thành công ra tệp Excel:\n{saveFileDialog.FileName}", "Xuất báo cáo");
                    RevitVersionStatus = "Đã xuất bảng tính Excel lúc " + DateTime.Now.ToString("HH:mm:ss");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowWarning($"Có lỗi xảy ra khi lưu file Excel:\n{ex.Message}", "Lỗi");
            }
        }

        private void ExecuteExportPdf()
        {
            try
            {
                var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Title = "Lưu báo cáo thuyết minh tính toán",
                    Filter = "Text Files (*.txt)|*.txt|CSV Files (*.csv)|*.csv",
                    DefaultExt = ".txt",
                    FileName = "ThuyetMinhTinhToan_VanKhuon.txt"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    // Tạo CalculationParameters từ dữ liệu hiện tại
                    var p = new CalculationParameters
                    {
                        WoodStrength = _SharedDataViewModel.WoodStrength > 0 ? _SharedDataViewModel.WoodStrength : 15.0,
                        PlywoodBendingStrength = _SharedDataViewModel.PlywoodBendingStrength > 0 ? _SharedDataViewModel.PlywoodBendingStrength : 10.0,
                        WoodShearStrength = _SharedDataViewModel.WoodShearStrength > 0 ? _SharedDataViewModel.WoodShearStrength : 2.0,
                        ElasticModulus = _SharedDataViewModel.ElasticModulus,
                        ConcreteWeight = _SharedDataViewModel.ConcreteWeight > 0 ? _SharedDataViewModel.ConcreteWeight : 25.0,
                        WoodFormworkWeight = _SharedDataViewModel.WoodFormworkWeight,
                        ConstructionLiveLoad = _SharedDataViewModel.ConstructionLiveLoad,
                        DynamicImpactFactor = _SharedDataViewModel.DynamicImpactFactor > 0 ? _SharedDataViewModel.DynamicImpactFactor : 1.3,
                        DeadLoadSafetyFactor = _SharedDataViewModel.DeadLoadSafetyFactor,
                        LiveLoadSafetyFactor = _SharedDataViewModel.LiveLoadSafetyFactor,
                        MoistureFactor = _SharedDataViewModel.MoistureFactor,
                        TemperatureFactor = _SharedDataViewModel.TemperatureFactor,
                        SlabDeflectionLimitRatio = FormworkCalculationService.ParseFraction(_SharedDataViewModel.SlabDeflectionLimit),
                        JoistDeflectionLimitRatio = FormworkCalculationService.ParseFraction(_SharedDataViewModel.JoistDeflectionLimit),
                        SlabThickness = _SharedDataViewModel.SlabThickness,
                        VanMatThickness = _SharedDataViewModel.VanMatThickness,
                        XaGoPhuWidth = _SharedDataViewModel.XaGoPhuWidth,
                        XaGoPhuHeight = _SharedDataViewModel.XaGoPhuHeight,
                        XaGoChinhWidth = _SharedDataViewModel.XaGoChinhWidth,
                        XaGoChinhHeight = _SharedDataViewModel.XaGoChinhHeight,
                        CayChongDiameter = _SharedDataViewModel.CayChongDiameter,
                        CayChongThickness = _SharedDataViewModel.CayChongThickness,
                        CayChongHeight = _SharedDataViewModel.CayChongHeight,
                        SupportEffectiveLengthFactor = _SharedDataViewModel.SupportEffectiveLengthFactor,
                        SupportRatedCapacity = _SharedDataViewModel.SupportRatedCapacity,
                        SupportSlendernessLimit = _SharedDataViewModel.SupportSlendernessLimit,
                        SupportBasePlateWidth = _SharedDataViewModel.SupportBasePlateWidth,
                        SupportBasePlateDepth = _SharedDataViewModel.SupportBasePlateDepth,
                        SupportBaseBearingAllowable = _SharedDataViewModel.SupportBaseBearingAllowable,
                        SupportSolePlateWidth = _SharedDataViewModel.SupportSolePlateWidth,
                        SupportSolePlateDepth = _SharedDataViewModel.SupportSolePlateDepth,
                        SupportGroundBearingAllowable = _SharedDataViewModel.SupportGroundBearingAllowable,
                        SupportHeadPlateWidth = _SharedDataViewModel.SupportHeadPlateWidth,
                        SupportHeadPlateDepth = _SharedDataViewModel.SupportHeadPlateDepth,
                        SupportHeadBearingAllowable = _SharedDataViewModel.SupportHeadBearingAllowable,
                        SupportJackExtension = _SharedDataViewModel.SupportJackExtension,
                        SupportJackExtensionLimit = _SharedDataViewModel.SupportJackExtensionLimit,
                        SelectedScaffoldSystems = _SharedDataViewModel.SelectedScaffoldSystems,
                        SoNhipVanMat = _SharedDataViewModel.SoNhipVanMat,
                        SoNhipXaGoPhu = _SharedDataViewModel.SoNhipXaGoPhu,
                        SoNhipXaGoChinh = _SharedDataViewModel.SoNhipXaGoChinh,
                        Joist1Spacing = _SharedDataViewModel.Joist1Spacing > 0 ? _SharedDataViewModel.Joist1Spacing : 400.0,
                        Joist2Spacing = _SharedDataViewModel.Joist2Spacing > 0 ? _SharedDataViewModel.Joist2Spacing : 1000.0,
                        SupportSpacing = _SharedDataViewModel.SupportSpacing > 0 ? _SharedDataViewModel.SupportSpacing : 1000.0,
                        IsGroundLevel = _SharedDataViewModel.IsGroundLevel,
                        ComponentType = _SharedDataViewModel.CurrentComponentType,
                        BeamWidth = _SharedDataViewModel.BeamWidth,
                        BeamDepth = _SharedDataViewModel.BeamDepth,
                        BeamLength = _SharedDataViewModel.BeamLength,
                        BeamSidePlywoodThickness = _SharedDataViewModel.BeamSidePlywoodThickness,
                        BeamBottomPlywoodThickness = _SharedDataViewModel.BeamBottomPlywoodThickness,
                        BeamSideYokeSpacing = _SharedDataViewModel.BeamSideYokeSpacing,
                        BeamBottomYokeSpacing = _SharedDataViewModel.BeamBottomYokeSpacing,
                        BeamPropSpacing = _SharedDataViewModel.BeamPropSpacing,
                        BeamSecondaryJoistWidth = _SharedDataViewModel.BeamSecondaryJoistWidth,
                        BeamSecondaryJoistDepth = _SharedDataViewModel.BeamSecondaryJoistDepth,
                        BeamPrimaryWalerWidth = _SharedDataViewModel.BeamPrimaryWalerWidth,
                        BeamPrimaryWalerDepth = _SharedDataViewModel.BeamPrimaryWalerDepth,
                        BeamPrimaryWalerSpacing = _SharedDataViewModel.BeamPrimaryWalerSpacing,
                        BeamScaffoldBayWidth = _SharedDataViewModel.BeamScaffoldBayWidth,
                        BeamTieRodDiameter = _SharedDataViewModel.BeamTieRodDiameter,
                        BeamTieRodSpacing = _SharedDataViewModel.BeamTieRodSpacing,
                        BeamTieRodAllowableCapacity = _SharedDataViewModel.BeamTieRodAllowableCapacity,
                        BeamEdgeSurfaceTieRodDiameter = _SharedDataViewModel.BeamEdgeSurfaceTieRodDiameter,
                        BeamEdgeSurfaceTieRodSpacing = _SharedDataViewModel.BeamEdgeSurfaceTieRodSpacing,
                        BeamEdgeSurfaceTieRodAnchorLength = _SharedDataViewModel.BeamEdgeSurfaceTieRodAnchorLength,
                        BeamEdgeSurfaceTieRodAllowableCapacity = _SharedDataViewModel.BeamEdgeSurfaceTieRodAllowableCapacity,
                        BeamSideBraceSpacing = _SharedDataViewModel.BeamSideBraceSpacing,
                        BeamSideBraceAngle = _SharedDataViewModel.BeamSideBraceAngle,
                        BeamSideBraceDiameter = _SharedDataViewModel.BeamSideBraceDiameter,
                        BeamSideBraceThickness = _SharedDataViewModel.BeamSideBraceThickness,
                        BeamSideBraceCountPerStation = _SharedDataViewModel.BeamSideBraceCountPerStation,
                        BeamSideBraceAllowableCapacity = _SharedDataViewModel.BeamSideBraceAllowableCapacity,
                        BeamPlacementModeIndex = _SharedDataViewModel.BeamPlacementModeIndex,
                        BeamStrippingStrengthPercent = _SharedDataViewModel.BeamStrippingStrengthPercent,
                        BeamVibratorOverlap = _SharedDataViewModel.BeamVibratorOverlap,
                        IsBeamSelected = true,
                        ColumnWidth = _SharedDataViewModel.ColumnWidth,
                        ColumnDepth = _SharedDataViewModel.ColumnDepth,
                        ColumnHeight = _SharedDataViewModel.ColumnHeight,
                        ColumnPlywoodThickness = _SharedDataViewModel.ColumnPlywoodThickness,
                        ColumnStiffenerWidth = _SharedDataViewModel.ColumnStiffenerWidth,
                        ColumnStiffenerDepth = _SharedDataViewModel.ColumnStiffenerDepth,
                        ColumnStiffenerSpacing = _SharedDataViewModel.ColumnStiffenerSpacing,
                        ColumnYokeSpacing = _SharedDataViewModel.ColumnYokeSpacing,
                        ColumnUseZonedYokeSpacing = _SharedDataViewModel.ColumnUseZonedYokeSpacing,
                        ColumnFirstYokeOffset = _SharedDataViewModel.ColumnFirstYokeOffset,
                        ColumnYokeBottomSpacing = _SharedDataViewModel.ColumnYokeBottomSpacing,
                        ColumnYokeUpperSpacing = _SharedDataViewModel.ColumnYokeUpperSpacing,
                        ColumnYokeTransitionHeight = _SharedDataViewModel.ColumnYokeTransitionHeight,
                        ColumnLastYokeOffset = _SharedDataViewModel.ColumnLastYokeOffset,
                        ColumnYokeWidth = _SharedDataViewModel.ColumnYokeWidth,
                        ColumnYokeHeight = _SharedDataViewModel.ColumnYokeHeight,
                        ColumnYokeThickness = _SharedDataViewModel.ColumnYokeThickness,
                        ColumnYokePairCount = _SharedDataViewModel.ColumnYokePairCount,
                        ColumnYokeMaterialIndex = _SharedDataViewModel.ColumnYokeMaterialIndex,
                        ColumnTieRodDiameter = _SharedDataViewModel.ColumnTieRodDiameter,
                        ColumnTieRodCountPerFace = _SharedDataViewModel.ColumnTieRodCountPerFace,
                        ColumnTieRodAllowableCapacity = _SharedDataViewModel.ColumnTieRodAllowableCapacity,
                        ColumnBraceCount = _SharedDataViewModel.ColumnBraceCount,
                        ColumnBraceAngle = _SharedDataViewModel.ColumnBraceAngle,
                        ColumnBraceDiameter = _SharedDataViewModel.ColumnBraceDiameter,
                        ColumnBraceThickness = _SharedDataViewModel.ColumnBraceThickness,
                        ColumnBraceAllowableCapacity = _SharedDataViewModel.ColumnBraceAllowableCapacity,
                        ColumnBracePlacementModeIndex = _SharedDataViewModel.ColumnBracePlacementModeIndex,
                        ColumnNeighbourTieThreshold = _SharedDataViewModel.ColumnNeighbourTieThreshold,
                        ColumnIsBoundaryOrCorner = _SharedDataViewModel.ColumnIsBoundaryOrCorner,
                        ColumnCableAllowableCapacity = _SharedDataViewModel.ColumnCableAllowableCapacity,
                        ColumnTurnbuckleAllowableCapacity = _SharedDataViewModel.ColumnTurnbuckleAllowableCapacity,
                        ColumnAnchorAllowableCapacity = _SharedDataViewModel.ColumnAnchorAllowableCapacity,
                        ColumnAxisOffset = _SharedDataViewModel.ColumnAxisOffset,
                        ColumnPlumbTargetTolerance = _SharedDataViewModel.ColumnPlumbTargetTolerance,
                        ColumnFormworkTopExtension = _SharedDataViewModel.ColumnFormworkTopExtension,
                        ColumnHoseBottomClearance = _SharedDataViewModel.ColumnHoseBottomClearance,
                        ColumnPourLiftHeight = _SharedDataViewModel.ColumnPourLiftHeight,
                        ColumnBeamStopOffset = _SharedDataViewModel.ColumnBeamStopOffset,
                        ColumnStripTimeHours = _SharedDataViewModel.ColumnStripTimeHours,
                        ColumnCuringDays = _SharedDataViewModel.ColumnCuringDays,
                        ColumnUseWorkPlatform = _SharedDataViewModel.ColumnUseWorkPlatform,
                        ColumnWorkPlatformWidth = _SharedDataViewModel.ColumnWorkPlatformWidth,
                        ColumnGuardrailHeight = _SharedDataViewModel.ColumnGuardrailHeight,
                        ConcretePourRate = _SharedDataViewModel.ConcretePourRate,
                        ConcreteTemperature = _SharedDataViewModel.ConcreteTemperature,
                        IsColumnSelected = true
                    };

                    Models.CalculationResult calcResult;
                    if (_SharedDataViewModel.CurrentComponentType == StructuralComponentType.Beam)
                    {
                        calcResult = FormworkCalculationService.CheckBeamSafety(p);
                    }
                    else if (_SharedDataViewModel.CurrentComponentType == StructuralComponentType.Column)
                    {
                        calcResult = FormworkCalculationService.CheckColumnSafety(p);
                    }
                    else
                    {
                        calcResult = FormworkCalculationService.CheckSafety(p);
                    }

                    // Export every explicit check, including support and connection accessories.
                    var listKetQua = new List<KetQuaTinhToan>();
                    if (_SharedDataViewModel.HasMixedComponentSelection)
                    {
                        bool originalGround = p.IsGroundLevel;
                        if (_SharedDataViewModel.SelectedSlabCount > 0)
                        {
                            p.IsGroundLevel = false;
                            listKetQua.AddRange(FormworkCalculationService.CheckSafety(p).GetAllChecks());
                        }
                        if (_SharedDataViewModel.SelectedFoundationCount > 0)
                        {
                            p.IsGroundLevel = true;
                            listKetQua.AddRange(FormworkCalculationService.CheckSafety(p).GetAllChecks());
                        }
                        p.IsGroundLevel = originalGround;
                        if (_SharedDataViewModel.SelectedBeamCount > 0)
                            listKetQua.AddRange(FormworkCalculationService.CheckBeamSafety(p).GetAllChecks());
                        if (_SharedDataViewModel.SelectedColumnCount > 0)
                            listKetQua.AddRange(FormworkCalculationService.CheckColumnSafety(p).GetAllChecks());
                    }
                    else
                    {
                        listKetQua.AddRange(calcResult.GetAllChecks());
                    }

                    string tieuChuanStr = _SharedDataViewModel.SelectedTieuChuan == 0 ? "TCVN 9356:2012 (Gỗ)" :
                                         (_SharedDataViewModel.SelectedTieuChuan == 1 ? "EN 1995-1-1 (Eurocode 5)" : "AWC NDS-2018");

                    var xuatKetQua = new XuatKetQua(saveFileDialog.FileName, "KietBT - 0306868", tieuChuanStr);
                    bool success = xuatKetQua.ExportToReportFile(listKetQua);

                    if (success)
                    {
                        _dialogService.ShowInfo($"Xuất thuyết minh tính toán thành công ra tệp:\n{saveFileDialog.FileName}", "Xuất báo cáo");
                        RevitVersionStatus = "Đã xuất thuyết minh lúc " + DateTime.Now.ToString("HH:mm:ss");
                    }
                    else
                    {
                        _dialogService.ShowWarning("Không thể xuất báo cáo. Vui lòng kiểm tra dữ liệu đầu vào.", "Lỗi");
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowWarning($"Có lỗi xảy ra khi xuất báo cáo:\n{ex.Message}", "Lỗi");
            }
        }

        private void ExecuteExportWord()
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Title = "Chọn file Word mẫu (Template.docx)",
                    Filter = "Word Files (*.docx)|*.docx",
                    DefaultExt = ".docx"
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    string templatePath = openFileDialog.FileName;

                    var saveFileDialog = new Microsoft.Win32.SaveFileDialog
                    {
                        Title = "Lưu báo cáo Word",
                        Filter = "Word Files (*.docx)|*.docx",
                        DefaultExt = ".docx",
                        FileName = "BaoCaoTinhToanVanKhuon.docx"
                    };

                    if (saveFileDialog.ShowDialog() == true)
                    {
                        string tieuChuanStr = _SharedDataViewModel.SelectedTieuChuan == 0 ? "TCVN 9356:2012 (Gỗ)" : 
                                             (_SharedDataViewModel.SelectedTieuChuan == 1 ? "EN 1995-1-1 (Eurocode 5)" : "AWC NDS-2018");

                        var value = new Dictionary<string, object>
                        {
                            ["TieuChuan"] = tieuChuanStr,
                            ["Joist1Spacing"] = Joist1Spacing,
                            ["Joist2Spacing"] = Joist2Spacing,
                            ["SupportSpacing"] = SupportSpacing,
                            ["TotalProjectArea"] = _SharedDataViewModel.KetQuaTotalProjectArea,
                            ["TotalProjectPerimeter"] = _SharedDataViewModel.KetQuaTotalProjectPerimeter,
                            ["MaterialQuantities"] = MaterialQuantities ?? new List<MaterialItem>(),
                            ["ProjectMaterialQuantities"] = ProjectMaterialQuantities ?? new List<MaterialItem>(),
                            ["FloorElements"] = FloorElements ?? new List<FloorElementModel>()
                        };

                        MiniWord.SaveAsByTemplate(saveFileDialog.FileName, templatePath, value);

                        _dialogService.ShowInfo($"Xuất báo cáo Word thành công ra tệp:\n{saveFileDialog.FileName}", "Xuất báo cáo Word");
                        RevitVersionStatus = "Đã xuất báo cáo Word lúc " + DateTime.Now.ToString("HH:mm:ss");
                    }
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowWarning($"Có lỗi xảy ra khi xuất file Word:\n{ex.Message}", "Lỗi");
            }
        }

        /// <summary>
        /// Xử lý sự kiện đồng bộ khi người dùng chọn sàn từ bất kỳ trang nào.
        /// Cập nhật danh sách sàn và bảng tiên lượng vật tư.
        /// </summary>
        private void OnSlabSelectionCompleted(object sender, SlabSelectionEventArgs e)
        {
            _isWholeProjectTakeoff = false;
            FloorElements = e.FloorElements;
            UpdateMaterialQuantities();
            UpdateProjectMaterialQuantities();
            RevitVersionStatus = $"Đồng bộ từ Revit: {e.FloorCount} sàn - DT: {e.SelectedSlabArea:F2} m² - Cập nhật lúc {DateTime.Now:HH:mm:ss}";
        }

        private void ExecuteChangeTab(object parameter)
        {
            _SharedDataViewModel.ExecuteChangeTab(parameter);
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void OnSharedDataViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (_SharedDataViewModel.IsApplyingValidatedDesign)
                return;

            OnPropertyChanged(e.PropertyName);

            if (e.PropertyName == nameof(SharedDataViewModel.ValidatedDesignRevision))
            {
                if (_SharedDataViewModel.KetQuaIsProjectCalculated && FloorElements != null && FloorElements.Count > 0)
                    RebuildWholeProjectTakeoffFromCommittedDesign();
                else
                {
                    _isWholeProjectTakeoff = false;
                    UpdateMaterialQuantities();
                    UpdateProjectMaterialQuantities();
                }
                _SharedDataViewModel.SynchronizeScheduleFromValidatedDesign();
                RevitVersionStatus = $"Đã đồng bộ thiết kế đạt chuẩn (phiên bản {_SharedDataViewModel.ValidatedDesignRevision}) lúc {DateTime.Now:HH:mm:ss}";
                return;
            }

            if (e.PropertyName == nameof(SharedDataViewModel.KetQuaPreviewControl))
            {
                OnPropertyChanged(nameof(ProjectPreview));
            }

            if (e.PropertyName == nameof(SharedDataViewModel.CurrentComponentType))
            {
                OnPropertyChanged(nameof(SelectedComponentQuantitiesTitle));
                OnPropertyChanged(nameof(ProjectComponentListTitle));
                OnPropertyChanged(nameof(Param1Label));
                OnPropertyChanged(nameof(Param2Label));
                OnPropertyChanged(nameof(Param3Label));
            }

            if (e.PropertyName == nameof(SharedDataViewModel.SelectedSlabArea) ||
                e.PropertyName == nameof(SharedDataViewModel.SelectedSlabPerimeter) ||
                e.PropertyName == nameof(SharedDataViewModel.SlabThickness) ||
                e.PropertyName == nameof(SharedDataViewModel.Joist1Spacing) ||
                e.PropertyName == nameof(SharedDataViewModel.Joist2Spacing) ||
                e.PropertyName == nameof(SharedDataViewModel.SupportSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.VanMatThickness) ||
                e.PropertyName == nameof(SharedDataViewModel.XaGoPhuWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.XaGoPhuHeight) ||
                e.PropertyName == nameof(SharedDataViewModel.XaGoChinhWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.XaGoChinhHeight) ||
                e.PropertyName == nameof(SharedDataViewModel.CayChongDiameter) ||
                e.PropertyName == nameof(SharedDataViewModel.CayChongThickness) ||
                e.PropertyName == nameof(SharedDataViewModel.SelectedScaffoldSystems) ||
                e.PropertyName == nameof(SharedDataViewModel.CurrentComponentType) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamDepth) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamLength) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamSidePlywoodThickness) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamBottomPlywoodThickness) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamSideYokeSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamBottomYokeSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.BeamPropSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnWidth) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnDepth) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnHeight) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnPlywoodThickness) ||
                e.PropertyName == nameof(SharedDataViewModel.ColumnYokeSpacing) ||
                e.PropertyName == nameof(SharedDataViewModel.ConcretePourRate) ||
                e.PropertyName == nameof(SharedDataViewModel.ConcreteTemperature))
            {
                UpdateMaterialQuantities();
                UpdateProjectMaterialQuantities();

                OnPropertyChanged(nameof(Param1Value));
                OnPropertyChanged(nameof(Param2Value));
                OnPropertyChanged(nameof(Param3Value));
            }
        }

        private void ExecuteShowFloorDetails(object parameter)
        {
            if (parameter is FloorElementModel floorModel)
            {
                ExecuteShowMultipleFloorDetails(new List<FloorElementModel> { floorModel });
            }
        }

        private void ExecuteShowMultipleFloorDetails(List<FloorElementModel> floorModels)
        {
            if (floorModels == null || floorModels.Count == 0) return;
            _isWholeProjectTakeoff = false;

            FloorElementModel first = floorModels[0];
            if (first.FamilyTypeName?.StartsWith("DẦM · ") == true)
            {
                _SharedDataViewModel.CurrentComponentType = StructuralComponentType.Beam;
                _SharedDataViewModel.BeamLength = Math.Max(100, first.StoryHeight);
                _SharedDataViewModel.BeamWidth = Math.Max(100, first.Elevation);
                _SharedDataViewModel.BeamDepth = Math.Max(100, first.Thickness);
            }
            else if (first.FamilyTypeName?.StartsWith("CỘT · ") == true)
            {
                _SharedDataViewModel.CurrentComponentType = StructuralComponentType.Column;
                _SharedDataViewModel.ColumnHeight = Math.Max(100, first.StoryHeight);
                _SharedDataViewModel.ColumnWidth = Math.Max(100, first.Elevation);
                _SharedDataViewModel.ColumnDepth = Math.Max(100, first.Thickness);
            }
            else if (first.FamilyTypeName?.StartsWith("MÓNG · ") == true)
            {
                _SharedDataViewModel.CurrentComponentType = StructuralComponentType.Foundation;
            }
            else
            {
                _SharedDataViewModel.CurrentComponentType = StructuralComponentType.Slab;
            }

            double totalArea = floorModels.Sum(f => f.Area);
            double totalPerimeter = floorModels.Sum(f => f.Perimeter);
            double thickness = floorModels.First().Thickness;

            _SharedDataViewModel.SelectedSlabArea = totalArea;
            _SharedDataViewModel.SelectedSlabPerimeter = totalPerimeter;

            if (_SharedDataViewModel.CurrentComponentType == StructuralComponentType.Foundation)
            {
                SelectedComponentQuantitiesTitle = "BẢNG TIÊN LƯỢNG VẬT TƯ / PHỤ KIỆN (MÓNG ĐANG CHỌN)";
                MaterialQuantities = CalculateFoundationQuantities(
                    Math.Max(100, first.StoryHeight), Math.Max(100, first.Elevation),
                    Math.Max(100, first.Thickness), totalPerimeter);
            }
            else UpdateMaterialQuantities();
            
            var validIds = floorModels
                .Where(f => !string.IsNullOrEmpty(f.UniqueId))
                .Select(f => f.UniqueId)
                .ToList();
                
            if (validIds.Count == 0)
            {
                _dialogService.ShowWarning("Dữ liệu sàn cũ chưa được cập nhật ID. Vui lòng bấm 'Thống kê toàn dự án' một lần nữa trước khi xem chi tiết.", "Cảnh báo dữ liệu");
                return;
            }

            _SharedDataViewModel.RenderPreviewGraphics(validIds, control => 
            {
                _SharedDataViewModel.KetQuaPreviewControl = control;
            });
        }

        private void UpdateMaterialQuantities()
        {
            SelectedComponentQuantitiesTitle = "BẢNG TIÊN LƯỢNG VẬT TƯ / PHỤ KIỆN (CẤU KIỆN ĐANG CHỌN)";
            List<FloorElementModel> selectedComponents = (_SharedDataViewModel.FloorElements ?? new List<FloorElementModel>())
                .Where(component => component.ComponentType == _SharedDataViewModel.CurrentComponentType)
                .ToList();
            if (selectedComponents.Count > 0)
            {
                MaterialQuantities = AggregateComponentTakeoff(selectedComponents);
                return;
            }
            if (_SharedDataViewModel.CurrentComponentType == StructuralComponentType.Beam)
            {
                double length = _SharedDataViewModel.BeamLength > 0 ? _SharedDataViewModel.BeamLength : 4000.0;
                MaterialQuantities = CalculateBeamQuantities(_SharedDataViewModel.BeamWidth, _SharedDataViewModel.BeamDepth, length);
            }
            else if (_SharedDataViewModel.CurrentComponentType == StructuralComponentType.Column)
            {
                double height = _SharedDataViewModel.ColumnHeight > 0 ? _SharedDataViewModel.ColumnHeight : 3600.0;
                MaterialQuantities = CalculateColumnQuantities(_SharedDataViewModel.ColumnWidth, _SharedDataViewModel.ColumnDepth, height);
            }
            else if (_SharedDataViewModel.CurrentComponentType == StructuralComponentType.Foundation)
            {
                double area = _SharedDataViewModel.SelectedSlabArea > 0 ? _SharedDataViewModel.SelectedSlabArea : 4.0;
                double side = Math.Sqrt(area) * 1000.0;
                double perimeter = _SharedDataViewModel.SelectedSlabPerimeter > 0 ? _SharedDataViewModel.SelectedSlabPerimeter : 4 * Math.Sqrt(area);
                MaterialQuantities = CalculateFoundationQuantities(side, side,
                    Math.Max(300, _SharedDataViewModel.SlabThickness), perimeter);
            }
            else
            {
                double area = _SharedDataViewModel.SelectedSlabArea > 0 ? _SharedDataViewModel.SelectedSlabArea : 100.0;
                double perimeter = _SharedDataViewModel.SelectedSlabPerimeter > 0 ? _SharedDataViewModel.SelectedSlabPerimeter : 40.0;
                double thicknessMm = _SharedDataViewModel.SlabThickness;
                MaterialQuantities = CalculateQuantities(area, perimeter, thicknessMm);
            }
        }

        private void RebuildWholeProjectTakeoffFromCommittedDesign()
        {
            ProjectMaterialQuantities = AggregateComponentTakeoff(FloorElements ?? new List<FloorElementModel>());
            MaterialQuantities = ProjectMaterialQuantities;
            _isWholeProjectTakeoff = true;
            SelectedComponentQuantitiesTitle = "BẢNG TIÊN LƯỢNG VẬT TƯ / HỆ CHỐNG / PHỤ KIỆN TOÀN DỰ ÁN";
        }

        private List<MaterialItem> AggregateComponentTakeoff(IEnumerable<FloorElementModel> components)
        {
            var takeoff = new Dictionary<string, MaterialItem>();
            foreach (FloorElementModel component in components ?? Enumerable.Empty<FloorElementModel>())
            {
                if (component == null) continue;
                CalculationParameters design = _SharedDataViewModel.GetEffectiveComponentParameters(
                    component, CreateTakeoffParameters());
                IEnumerable<MaterialItem> items;
                switch (component.ComponentType)
                {
                    case StructuralComponentType.Foundation:
                        items = CalculateFoundationQuantities(Math.Max(100, component.StoryHeight),
                            Math.Max(100, component.Elevation), Math.Max(100, component.Thickness),
                            component.Perimeter, design);
                        break;
                    case StructuralComponentType.Beam:
                        items = CalculateBeamQuantities(Math.Max(100, component.Elevation),
                            Math.Max(100, component.Thickness), Math.Max(100, component.StoryHeight), design);
                        break;
                    case StructuralComponentType.Column:
                        items = CalculateColumnQuantities(Math.Max(100, component.Elevation),
                            Math.Max(100, component.Thickness), Math.Max(100, component.StoryHeight), design);
                        break;
                    default:
                        items = CalculateQuantities(Math.Max(0, component.Area), Math.Max(0, component.Perimeter),
                            Math.Max(50, component.Thickness), design);
                        break;
                }

                foreach (MaterialItem item in items.Where(item => item != null && item.Quantity > 0))
                {
                    string key = $"{item.MaterialName}|{item.Specification}|{item.Unit}";
                    if (takeoff.TryGetValue(key, out MaterialItem existing))
                        existing.Quantity += item.Quantity;
                    else
                        takeoff[key] = new MaterialItem
                        {
                            MaterialName = item.MaterialName,
                            Specification = item.Specification,
                            Quantity = item.Quantity,
                            Unit = item.Unit
                        };
                }
            }
            return takeoff.Values.OrderBy(item => item.MaterialName).ThenBy(item => item.Specification).ToList();
        }

        private void UpdateProjectMaterialQuantities()
        {
            if (_isWholeProjectTakeoff) return;
            if (!_SharedDataViewModel.KetQuaIsProjectCalculated || _SharedDataViewModel.KetQuaTotalProjectArea <= 0) return;

            // A project scan supplies actual elements, so aggregate their own committed
            // designs first. The type-level equivalent quantities below remain only as
            // a compatibility fallback for legacy sessions without element records.
            List<FloorElementModel> projectComponents = (_SharedDataViewModel.FloorElements ??
                new List<FloorElementModel>()).Where(component => component != null).ToList();
            if (projectComponents.Count > 0)
            {
                ProjectMaterialQuantities = AggregateComponentTakeoff(projectComponents);
                return;
            }

            if (_SharedDataViewModel.CurrentComponentType == StructuralComponentType.Beam)
            {
                double totalLengthM = _SharedDataViewModel.KetQuaTotalProjectArea;
                // Approximate total properties by passing an aggregated "equivalent" beam
                ProjectMaterialQuantities = CalculateBeamQuantities(_SharedDataViewModel.BeamWidth, _SharedDataViewModel.BeamDepth, totalLengthM * 1000.0);
            }
            else if (_SharedDataViewModel.CurrentComponentType == StructuralComponentType.Column)
            {
                double totalHeightM = _SharedDataViewModel.KetQuaTotalProjectArea;
                int count = (int)_SharedDataViewModel.KetQuaTotalProjectPerimeter;
                ProjectMaterialQuantities = CalculateColumnQuantities(_SharedDataViewModel.ColumnWidth, _SharedDataViewModel.ColumnDepth, totalHeightM * 1000.0);
                
                // Adjust per count items
                var countItem = ProjectMaterialQuantities.FirstOrDefault(m => m.MaterialName.Contains("Bulong"));
                if (countItem != null) countItem.Quantity = Math.Ceiling((totalHeightM * 1000.0 / _SharedDataViewModel.ColumnYokeSpacing) * 2);
            }
            else
            {
                double area = _SharedDataViewModel.KetQuaTotalProjectArea;
                double perimeter = _SharedDataViewModel.KetQuaTotalProjectPerimeter;
                double thicknessMm = _SharedDataViewModel.SlabThickness;
                ProjectMaterialQuantities = CalculateQuantities(area, perimeter, thicknessMm);
            }
        }

        private CalculationParameters CreateTakeoffParameters()
        {
            return _SharedDataViewModel.ValidatedDesignParameters ?? new CalculationParameters
            {
                VanMatThickness = _SharedDataViewModel.VanMatThickness,
                XaGoPhuWidth = _SharedDataViewModel.XaGoPhuWidth,
                XaGoPhuHeight = _SharedDataViewModel.XaGoPhuHeight,
                XaGoChinhWidth = _SharedDataViewModel.XaGoChinhWidth,
                XaGoChinhHeight = _SharedDataViewModel.XaGoChinhHeight,
                Joist1Spacing = _SharedDataViewModel.Joist1Spacing,
                Joist2Spacing = _SharedDataViewModel.Joist2Spacing,
                SupportSpacing = _SharedDataViewModel.SupportSpacing,
                CayChongDiameter = _SharedDataViewModel.CayChongDiameter,
                CayChongThickness = _SharedDataViewModel.CayChongThickness,
                SupportJackExtensionLimit = _SharedDataViewModel.SupportJackExtensionLimit,
                SupportHeadPlateWidth = _SharedDataViewModel.SupportHeadPlateWidth,
                SupportHeadPlateDepth = _SharedDataViewModel.SupportHeadPlateDepth,
                SupportBasePlateWidth = _SharedDataViewModel.SupportBasePlateWidth,
                SupportBasePlateDepth = _SharedDataViewModel.SupportBasePlateDepth,
                SupportSolePlateWidth = _SharedDataViewModel.SupportSolePlateWidth,
                SupportSolePlateDepth = _SharedDataViewModel.SupportSolePlateDepth,
                SelectedScaffoldSystems = _SharedDataViewModel.SelectedScaffoldSystems,
                BeamBottomPlywoodThickness = _SharedDataViewModel.BeamBottomPlywoodThickness,
                BeamSidePlywoodThickness = _SharedDataViewModel.BeamSidePlywoodThickness,
                BeamBottomYokeSpacing = _SharedDataViewModel.BeamBottomYokeSpacing,
                BeamSideYokeSpacing = _SharedDataViewModel.BeamSideYokeSpacing,
                BeamPropSpacing = _SharedDataViewModel.BeamPropSpacing,
                BeamSecondaryJoistWidth = _SharedDataViewModel.BeamSecondaryJoistWidth,
                BeamSecondaryJoistDepth = _SharedDataViewModel.BeamSecondaryJoistDepth,
                BeamPrimaryWalerWidth = _SharedDataViewModel.BeamPrimaryWalerWidth,
                BeamPrimaryWalerDepth = _SharedDataViewModel.BeamPrimaryWalerDepth,
                BeamTieRodDiameter = _SharedDataViewModel.BeamTieRodDiameter,
                BeamTieRodSpacing = _SharedDataViewModel.BeamTieRodSpacing,
                BeamTieRodAllowableCapacity = _SharedDataViewModel.BeamTieRodAllowableCapacity,
                BeamEdgeSurfaceTieRodDiameter = _SharedDataViewModel.BeamEdgeSurfaceTieRodDiameter,
                BeamEdgeSurfaceTieRodSpacing = _SharedDataViewModel.BeamEdgeSurfaceTieRodSpacing,
                BeamEdgeSurfaceTieRodAnchorLength = _SharedDataViewModel.BeamEdgeSurfaceTieRodAnchorLength,
                BeamEdgeSurfaceTieRodAllowableCapacity = _SharedDataViewModel.BeamEdgeSurfaceTieRodAllowableCapacity,
                BeamSideBraceSpacing = _SharedDataViewModel.BeamSideBraceSpacing,
                BeamSideBraceAngle = _SharedDataViewModel.BeamSideBraceAngle,
                BeamSideBraceDiameter = _SharedDataViewModel.BeamSideBraceDiameter,
                BeamSideBraceThickness = _SharedDataViewModel.BeamSideBraceThickness,
                BeamSideBraceCountPerStation = _SharedDataViewModel.BeamSideBraceCountPerStation,
                BeamSideBraceAllowableCapacity = _SharedDataViewModel.BeamSideBraceAllowableCapacity,
                BeamPlacementModeIndex = _SharedDataViewModel.BeamPlacementModeIndex,
                ColumnPlywoodThickness = _SharedDataViewModel.ColumnPlywoodThickness,
                ColumnStiffenerWidth = _SharedDataViewModel.ColumnStiffenerWidth,
                ColumnStiffenerDepth = _SharedDataViewModel.ColumnStiffenerDepth,
                ColumnStiffenerSpacing = _SharedDataViewModel.ColumnStiffenerSpacing,
                ColumnYokeSpacing = _SharedDataViewModel.ColumnYokeSpacing,
                ColumnYokeBottomSpacing = _SharedDataViewModel.ColumnYokeBottomSpacing,
                ColumnYokeUpperSpacing = _SharedDataViewModel.ColumnYokeUpperSpacing,
                ColumnYokeWidth = _SharedDataViewModel.ColumnYokeWidth,
                ColumnYokeHeight = _SharedDataViewModel.ColumnYokeHeight,
                ColumnYokeThickness = _SharedDataViewModel.ColumnYokeThickness,
                ColumnYokePairCount = _SharedDataViewModel.ColumnYokePairCount,
                ColumnTieRodDiameter = _SharedDataViewModel.ColumnTieRodDiameter,
                ColumnTieRodCountPerFace = _SharedDataViewModel.ColumnTieRodCountPerFace,
                ColumnBraceCount = _SharedDataViewModel.ColumnBraceCount,
                ColumnBraceDiameter = _SharedDataViewModel.ColumnBraceDiameter,
                ColumnBraceThickness = _SharedDataViewModel.ColumnBraceThickness,
                ColumnBraceAllowableCapacity = _SharedDataViewModel.ColumnBraceAllowableCapacity,
                ColumnCableAllowableCapacity = _SharedDataViewModel.ColumnCableAllowableCapacity,
                ColumnTurnbuckleAllowableCapacity = _SharedDataViewModel.ColumnTurnbuckleAllowableCapacity,
                ColumnAnchorAllowableCapacity = _SharedDataViewModel.ColumnAnchorAllowableCapacity,
                ColumnUseWorkPlatform = _SharedDataViewModel.ColumnUseWorkPlatform,
                ColumnWorkPlatformWidth = _SharedDataViewModel.ColumnWorkPlatformWidth,
                ColumnGuardrailHeight = _SharedDataViewModel.ColumnGuardrailHeight
            };
        }

        private static bool UsesMultipleScaffoldSystems(CalculationParameters parameters)
        {
            ScaffoldSystemType systems = parameters.SelectedScaffoldSystems.Normalize();
            return systems != ScaffoldSystemType.Frame &&
                   systems != ScaffoldSystemType.Wedge &&
                   systems != ScaffoldSystemType.Ringlock;
        }

        private static string ScaffoldSupportMaterialName(CalculationParameters parameters,
            string scope)
        {
            string systems = parameters.SelectedScaffoldSystems.ToDisplayText();
            return UsesMultipleScaffoldSystems(parameters)
                ? $"Nút chống quy đổi {scope} · nhiều hệ theo vùng ({systems})"
                : $"Chống đứng {scope} · {systems}";
        }

        private static string ScaffoldSupportSpecification(CalculationParameters parameters)
        {
            string specification =
                $"Tiết diện tính quy đổi Ø{parameters.CayChongDiameter:F0} x " +
                $"{parameters.CayChongThickness:F1} mm; [P]={parameters.SupportRatedCapacity:F1} kN";
            return UsesMultipleScaffoldSystems(parameters)
                ? specification + "; phân bổ từng hệ theo vùng 3D, xác nhận module/[P]/liên kết theo catalogue nhà sản xuất"
                : specification + "; xác nhận module và [P] theo catalogue nhà sản xuất";
        }

        private static string ScaffoldSupportUnit(CalculationParameters parameters)
        {
            ScaffoldSystemType systems = parameters.SelectedScaffoldSystems.Normalize();
            if (UsesMultipleScaffoldSystems(parameters)) return "Nút";
            // The current estimator counts bearing nodes, not complete paired H frames.
            // Calling this quantity "Khung" or "Cây" would overstate BOM precision.
            return systems == ScaffoldSystemType.Frame ? "Vị trí" : "Cây";
        }

        private List<MaterialItem> CalculateQuantities(double area, double perimeter, double thicknessMm,
            CalculationParameters design = null)
        {
            CalculationParameters p = design?.Clone() ?? CreateTakeoffParameters();
            double thicknessM = thicknessMm / 1000.0;

            double matSanArea = p.HasIntersectionContext ? p.NetBottomFormworkAreaM2 : area;
            double netSideArea = p.HasIntersectionContext
                ? p.NetSideFormworkAreaM2 + p.NetInternalOpeningSideAreaM2
                : perimeter * thicknessM;

            double plywoodMatSan = Math.Ceiling(matSanArea / (1.22 * 2.44) * 1.10); 
            double lengthThanhSan = Math.Ceiling(netSideArea / Math.Max(0.001, thicknessM) * 1.15); 

            double joist1Len = Math.Ceiling(matSanArea * (1000.0 / Math.Max(1, p.Joist1Spacing)) * 1.05); 
            double joist2Len = Math.Ceiling(matSanArea * (1000.0 / Math.Max(1, p.Joist2Spacing)) * 1.05);
            
            double propsPerM2 = 1.0 / ((Math.Max(1, p.Joist2Spacing) / 1000.0) * (Math.Max(1, p.SupportSpacing) / 1000.0));
            double propCount = p.IsGroundLevel ? 0 : Math.Ceiling(matSanArea * propsPerM2 * 1.05);
            double diagonalBraceCount = Math.Ceiling(propCount / 6.0) * 2;
            string scaffoldSystems = p.SelectedScaffoldSystems.ToDisplayText();
            string supportUnit = ScaffoldSupportUnit(p);
            return new List<MaterialItem>
            {
                new MaterialItem { MaterialName = $"Ván khuôn mặt sàn (Phủ phim)", Specification = $"{p.VanMatThickness:F0} x 1220 x 2440 mm", Quantity = plywoodMatSan, Unit = "Tấm" },
                new MaterialItem { MaterialName = $"Ván khuôn thành sàn (Phủ phim)", Specification = $"{p.VanMatThickness:F0} x {thicknessMm:F0} mm", Quantity = lengthThanhSan, Unit = "m" },
                new MaterialItem { MaterialName = $"Gỗ xà gồ phụ (Lớp 1)", Specification = $"{p.XaGoPhuWidth:F0} x {p.XaGoPhuHeight:F0} mm", Quantity = joist1Len, Unit = "m" },
                new MaterialItem { MaterialName = $"Gỗ xà gồ chính (Lớp 2)", Specification = $"{p.XaGoChinhWidth:F0} x {p.XaGoChinhHeight:F0} mm", Quantity = joist2Len, Unit = "m" },
                new MaterialItem { MaterialName = ScaffoldSupportMaterialName(p, "sàn"), Specification = ScaffoldSupportSpecification(p), Quantity = propCount, Unit = supportUnit },
                new MaterialItem { MaterialName = $"Kích chân · {scaffoldSystems}", Specification = $"Độ vươn ≤ {p.SupportJackExtensionLimit:F0} mm; đúng loại đầu nối của hệ", Quantity = propCount, Unit = "Cái" },
                new MaterialItem { MaterialName = $"Kích đầu U · {scaffoldSystems}", Specification = $"Bản đầu {p.SupportHeadPlateWidth:F0}×{p.SupportHeadPlateDepth:F0} mm; đúng loại đầu nối của hệ", Quantity = propCount, Unit = "Cái" },
                new MaterialItem { MaterialName = $"Bản đế hệ chống · {scaffoldSystems}", Specification = $"{p.SupportBasePlateWidth:F0}×{p.SupportBasePlateDepth:F0} mm", Quantity = propCount, Unit = "Cái" },
                new MaterialItem { MaterialName = $"Tấm kê hệ chống · {scaffoldSystems}", Specification = $"{p.SupportSolePlateWidth:F0}×{p.SupportSolePlateDepth:F0} mm", Quantity = propCount, Unit = "Tấm" },
                new MaterialItem { MaterialName = $"Thanh giằng chéo · {scaffoldSystems}", Specification = "Số lượng quy đổi từ lưới chống; quy cách theo catalogue nhà sản xuất", Quantity = diagonalBraceCount, Unit = "Thanh" },
                new MaterialItem { MaterialName = $"Liên kết/khóa giằng · {scaffoldSystems}", Specification = "Dùng đúng chốt nêm/đĩa/khóa của từng hệ; không nối lẫn hệ", Quantity = diagonalBraceCount * 2, Unit = "Cái" }
            }.Where(item => item.Quantity > 0).ToList();
        }

        private List<MaterialItem> CalculateFoundationQuantities(
            double lengthMm, double widthMm, double depthMm, double perimeterM,
            CalculationParameters design = null)
        {
            CalculationParameters p = design?.Clone() ?? CreateTakeoffParameters();
            double lengthM = Math.Max(0.1, lengthMm / 1000.0);
            double widthM = Math.Max(0.1, widthMm / 1000.0);
            double depthM = Math.Max(0.1, depthMm / 1000.0);
            double perimeter = perimeterM > 0 ? perimeterM : 2 * (lengthM + widthM);
            double sideArea = p.HasIntersectionContext ? p.NetSideFormworkAreaM2 : perimeter * depthM;
            double topArea = p.HasIntersectionContext ? p.NetTopFormworkAreaM2 : lengthM * widthM;
            double plywood = Math.Ceiling((sideArea + topArea) / (1.22 * 2.44) * 1.10);
            double walerLevels = Math.Max(2, Math.Ceiling(depthMm / Math.Max(100, p.ColumnYokeBottomSpacing)));
            double walerLength = Math.Ceiling(perimeter * walerLevels * 1.05);
            double clampCount = Math.Ceiling(perimeter / 0.6) * walerLevels;
            double stakeCount = Math.Max(4, Math.Ceiling(perimeter / 1.0));
            return new List<MaterialItem>
            {
                new MaterialItem { MaterialName = "Ván khuôn thành móng (Phủ phim)", Specification = $"{p.VanMatThickness:F0}×1220×2440 mm", Quantity = plywood, Unit = "Tấm" },
                new MaterialItem { MaterialName = "Nẹp/xà gồ thành móng", Specification = $"{p.XaGoPhuWidth:F0}×{p.XaGoPhuHeight:F0} mm", Quantity = walerLength, Unit = "m" },
                new MaterialItem { MaterialName = "Kẹp/gông ván khuôn móng", Specification = "Kẹp góc + khóa nêm", Quantity = clampCount, Unit = "Bộ" },
                new MaterialItem { MaterialName = "Cọc chống/nêm giữ thành móng", Specification = $"Ống D{p.BeamSideBraceDiameter:F0}×{p.BeamSideBraceThickness:F1} mm", Quantity = stakeCount, Unit = "Cây" },
                new MaterialItem { MaterialName = "Thanh chống xiên móng", Specification = "Điều chỉnh ren", Quantity = stakeCount, Unit = "Cây" },
                new MaterialItem { MaterialName = "Bản đế chống móng", Specification = $"{p.SupportBasePlateWidth:F0}×{p.SupportBasePlateDepth:F0} mm", Quantity = stakeCount, Unit = "Cái" },
                new MaterialItem { MaterialName = "Neo/chốt giữ cốp pha móng", Specification = "Neo thép + khóa", Quantity = stakeCount * 2, Unit = "Cái" }
            };
        }

        private List<MaterialItem> CalculateBeamQuantities(double widthMm, double depthMm, double lengthMm,
            CalculationParameters design = null)
        {
            CalculationParameters p = design?.Clone() ?? CreateTakeoffParameters();
            lengthMm = p.HasIntersectionContext ? p.NetFormworkLengthMm : lengthMm;
            double widthM = widthMm / 1000.0;
            double depthM = depthMm / 1000.0;
            double lengthM = lengthMm / 1000.0;

            double bottomPlywoodArea = widthM * lengthM;
            double sidePlywoodArea = depthM * lengthM * 2;
            
            double bottomPlywoodCount = Math.Ceiling(bottomPlywoodArea / (1.22 * 2.44) * 1.10);
            double sidePlywoodCount = Math.Ceiling(sidePlywoodArea / (1.22 * 2.44) * 1.10);

            double bottomYokeCount = Math.Ceiling(lengthMm / Math.Max(1, p.BeamBottomYokeSpacing));
            double sideYokeCount = Math.Ceiling(lengthMm / Math.Max(1, p.BeamSideYokeSpacing));
            double propCount = p.IsGroundLevel
                ? 0
                : Math.Ceiling(lengthMm / Math.Max(1, p.BeamPropSpacing)) * 2;
            double secondaryJoistLength = Math.Ceiling(bottomYokeCount * widthM * 1.10);
            double primaryWalerLength = Math.Ceiling(lengthM * 2 * 1.05);
            // Hai hàng ty xuyên thành xuất hiện ở cả dầm giữa và dầm biên.
            // Ty chết trên mặt dầm biên được thống kê riêng bên dưới.
            double tieRodCount = Math.Ceiling(lengthMm /
                Math.Max(75, p.BeamTieRodSpacing)) * 2;
            // Edge-only accessories are counted only after explicit/manual or
            // intersection-based perimeter classification (mode 2).
            double sideBraceStations = p.BeamPlacementModeIndex == 2
                ? Math.Ceiling(lengthMm / Math.Max(300, p.BeamSideBraceSpacing))
                : 0;
            double sideBraceCount = sideBraceStations * Math.Max(1, p.BeamSideBraceCountPerStation);
            double surfaceTieCount = p.BeamPlacementModeIndex == 2
                ? Math.Ceiling(lengthMm / Math.Max(300, p.BeamEdgeSurfaceTieRodSpacing))
                : 0;
            string scaffoldSystems = p.SelectedScaffoldSystems.ToDisplayText();
            string supportUnit = ScaffoldSupportUnit(p);

            var items = new List<MaterialItem>
            {
                new MaterialItem { MaterialName = $"Ván khuôn đáy dầm (Phủ phim)", Specification = $"{p.BeamBottomPlywoodThickness:F0} x 1220 x 2440 mm", Quantity = bottomPlywoodCount > 0 ? bottomPlywoodCount : 1, Unit = "Tấm" },
                new MaterialItem { MaterialName = $"Ván khuôn thành dầm (Phủ phim)", Specification = $"{p.BeamSidePlywoodThickness:F0} x 1220 x 2440 mm", Quantity = sidePlywoodCount > 0 ? sidePlywoodCount : 1, Unit = "Tấm" },
                new MaterialItem { MaterialName = "Xà gồ phụ đáy dầm", Specification = $"{p.BeamSecondaryJoistWidth:F0}×{p.BeamSecondaryJoistDepth:F0} mm", Quantity = secondaryJoistLength, Unit = "m" },
                new MaterialItem { MaterialName = "Xà gồ chính dầm", Specification = $"{p.BeamPrimaryWalerWidth:F0}×{p.BeamPrimaryWalerDepth:F0} mm", Quantity = primaryWalerLength, Unit = "m" },
                new MaterialItem { MaterialName = $"Gông/kẹp thành dầm", Specification = $"@{p.BeamSideYokeSpacing:F0} mm", Quantity = sideYokeCount * 2, Unit = "Bộ" },
                new MaterialItem { MaterialName = ScaffoldSupportMaterialName(p, "dầm"), Specification = ScaffoldSupportSpecification(p), Quantity = propCount, Unit = supportUnit },
                new MaterialItem { MaterialName = $"Kích chân chống dầm · {scaffoldSystems}", Specification = $"Độ vươn ≤ {p.SupportJackExtensionLimit:F0} mm; đúng loại đầu nối của hệ", Quantity = propCount, Unit = "Cái" },
                new MaterialItem { MaterialName = $"Kích đầu U dầm · {scaffoldSystems}", Specification = $"{p.SupportHeadPlateWidth:F0}×{p.SupportHeadPlateDepth:F0} mm; đúng loại đầu nối của hệ", Quantity = propCount, Unit = "Cái" },
                new MaterialItem { MaterialName = $"Bản đế/tấm kê chống dầm · {scaffoldSystems}", Specification = $"{p.SupportBasePlateWidth:F0}×{p.SupportBasePlateDepth:F0} mm", Quantity = propCount, Unit = "Bộ" }
            };
            if (tieRodCount > 0)
            {
                items.Add(new MaterialItem { MaterialName = "Ty giằng xuyên thành dầm", Specification = $"D{p.BeamTieRodDiameter:F0}", Quantity = tieRodCount, Unit = "Thanh" });
                items.Add(new MaterialItem { MaterialName = "Bát chuồn/đai ốc ty dầm", Specification = $"Cho ty D{p.BeamTieRodDiameter:F0}", Quantity = tieRodCount * 2, Unit = "Cái" });
            }
            if (sideBraceCount > 0)
            {
                items.Add(new MaterialItem { MaterialName = "Chống xiên thành dầm biên", Specification = $"D{p.BeamSideBraceDiameter:F0}×{p.BeamSideBraceThickness:F1} mm", Quantity = sideBraceCount, Unit = "Cây" });
                items.Add(new MaterialItem { MaterialName = "Bản đế chống xiên dầm", Specification = $"{p.SupportBasePlateWidth:F0}×{p.SupportBasePlateDepth:F0} mm", Quantity = sideBraceStations, Unit = "Cái" });
            }
            if (surfaceTieCount > 0)
            {
                items.Add(new MaterialItem
                {
                    MaterialName = "Ty chết cố định trên mặt dầm biên",
                    Specification = $"D{p.BeamEdgeSurfaceTieRodDiameter:F0} · L neo {p.BeamEdgeSurfaceTieRodAnchorLength:F0} mm @ {p.BeamEdgeSurfaceTieRodSpacing:F0} mm",
                    Quantity = surfaceTieCount,
                    Unit = "Thanh"
                });
                items.Add(new MaterialItem
                {
                    MaterialName = "Bát/đĩa neo ty chết dầm biên",
                    Specification = $"Cho ty D{p.BeamEdgeSurfaceTieRodDiameter:F0}",
                    Quantity = surfaceTieCount * 2,
                    Unit = "Cái"
                });
            }
            return items.Where(item => item.Quantity > 0).ToList();
        }

        private List<MaterialItem> CalculateColumnQuantities(double widthMm, double depthMm, double heightMm,
            CalculationParameters design = null)
        {
            CalculationParameters p = design?.Clone() ?? CreateTakeoffParameters();
            FormworkCalculationService.NormalizeColumnYokeLayout(p);
            double widthM = widthMm / 1000.0;
            double depthM = depthMm / 1000.0;
            double heightM = heightMm / 1000.0;

            double grossPlywoodArea = (widthM + depthM) * 2 * heightM;
            double plywoodArea = p.HasIntersectionContext ? p.NetSideFormworkAreaM2 : grossPlywoodArea;
            double plywoodCount = Math.Ceiling(plywoodArea / (1.22 * 2.44) * 1.10);

            p.ColumnHeight = heightMm;
            double yokeCount = FormworkCalculationService.BuildColumnYokeElevations(p).Count;
            double stiffenersPerimeter = 2 * (Math.Ceiling(widthMm / Math.Max(75, p.ColumnStiffenerSpacing)) + 1) +
                                         2 * (Math.Ceiling(depthMm / Math.Max(75, p.ColumnStiffenerSpacing)) + 1);
            double exposedRatio = grossPlywoodArea > 1e-9 ? Math.Min(1.0, plywoodArea / grossPlywoodArea) : 1.0;
            double stiffenerLength = Math.Ceiling(stiffenersPerimeter * heightM * exposedRatio * 1.05);
            double tieCount = yokeCount * 2 * Math.Max(1, p.ColumnTieRodCountPerFace);
            int supportedBraceDirections = Math.Max(0, Math.Min(4,
                p.ColumnSupportedBraceDirectionCount));
            // Material quantities must follow the same verified geometry as placement.
            // A zero-bearing column receives no fictitious push-pull prop quantity; its
            // missing directions remain in the alternative-restraint takeoff/warning.
            double braceCount = supportedBraceDirections == 0
                ? 0
                : Math.Max(supportedBraceDirections, p.ColumnBraceCount);
            int openDirections = p.ColumnIsBoundaryOrCorner
                ? Math.Max(1, 4 - supportedBraceDirections)
                : 0;
            double alternativeRestraintCount = openDirections == 0 ? 0 :
                Math.Max(3, openDirections * Math.Max(1, Math.Ceiling(braceCount / 4.0)));

            return new List<MaterialItem>
            {
                new MaterialItem { MaterialName = $"Ván khuôn cột (Phủ phim)", Specification = $"{p.ColumnPlywoodThickness:F0} x 1220 x 2440 mm", Quantity = plywoodCount > 0 ? plywoodCount : 1, Unit = "Tấm" },
                new MaterialItem { MaterialName = "Nẹp đứng cột", Specification = $"{p.ColumnStiffenerWidth:F0}×{p.ColumnStiffenerDepth:F0} mm", Quantity = stiffenerLength, Unit = "m" },
                new MaterialItem { MaterialName = $"Gông cột", Specification = $"{p.ColumnYokeWidth:F0}×{p.ColumnYokeHeight:F0}×{p.ColumnYokeThickness:F1} mm · chân @{p.ColumnYokeBottomSpacing:F0}/trên @{p.ColumnYokeUpperSpacing:F0}", Quantity = yokeCount * Math.Max(1, p.ColumnYokePairCount), Unit = "Bộ" },
                new MaterialItem { MaterialName = "Ty giằng gông cột", Specification = $"D{p.ColumnTieRodDiameter:F0}", Quantity = tieCount, Unit = "Thanh" },
                new MaterialItem { MaterialName = "Bát chuồn/đai ốc gông cột", Specification = $"Cho ty D{p.ColumnTieRodDiameter:F0}", Quantity = tieCount * 2, Unit = "Cái" },
                new MaterialItem
                {
                    MaterialName = "Chống đẩy-kéo cột",
                    Specification = $"D{p.ColumnBraceDiameter:F0}×{p.ColumnBraceThickness:F1} mm · " +
                                    $"{(p.ColumnIsBoundaryOrCorner ? "cột biên/góc" : "cột giữa")} · " +
                                    $"cấp tải {p.ColumnBraceAllowableCapacity:F0} kN",
                    Quantity = braceCount,
                    Unit = "Cây"
                },
                new MaterialItem { MaterialName = "Bản đế chống cột", Specification = $"{p.SupportBasePlateWidth:F0}×{p.SupportBasePlateDepth:F0} mm", Quantity = braceCount, Unit = "Cái" },
                new MaterialItem { MaterialName = "Cáp giằng cột biên/góc", Specification = $"[T]={p.ColumnCableAllowableCapacity:F0} kN", Quantity = alternativeRestraintCount, Unit = "Sợi" },
                new MaterialItem { MaterialName = "Tăng đơ cáp cột", Specification = $"[T]={p.ColumnTurnbuckleAllowableCapacity:F0} kN", Quantity = alternativeRestraintCount, Unit = "Cái" },
                new MaterialItem { MaterialName = "Neo chờ/bulldog anchor", Specification = $"[N]={p.ColumnAnchorAllowableCapacity:F0} kN", Quantity = alternativeRestraintCount, Unit = "Cái" },
                new MaterialItem { MaterialName = "Mâm thao tác cột", Specification = $"Rộng {p.ColumnWorkPlatformWidth:F0} mm", Quantity = p.ColumnUseWorkPlatform ? 1 : 0, Unit = "Bộ" },
                new MaterialItem { MaterialName = "Lan can an toàn cột", Specification = $"Cao {p.ColumnGuardrailHeight:F0} mm", Quantity = p.ColumnUseWorkPlatform ? 1 : 0, Unit = "Bộ" }
            };
        }
    }

}
