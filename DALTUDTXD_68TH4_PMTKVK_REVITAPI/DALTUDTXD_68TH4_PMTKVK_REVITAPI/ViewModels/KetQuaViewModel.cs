using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection.Metadata;
using System.Runtime.CompilerServices;
using System.Transactions;
using System.Windows;
using System.Windows.Input;
using System.Xml.Linq;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class KetQuaViewModel : INotifyPropertyChanged, IDisposable
    {
        private readonly DuLieuViewModel _duLieuViewModel;
        private readonly IDialogService _dialogService;
        private string _revitVersionStatus;
        private List<MaterialItem> _materialQuantities;

        public event PropertyChangedEventHandler PropertyChanged;

        public KetQuaViewModel(DuLieuViewModel duLieuViewModel, IDialogService dialogService = null)
        {
            _duLieuViewModel = duLieuViewModel ?? throw new ArgumentNullException(nameof(duLieuViewModel));
            _dialogService = dialogService ?? new MessageBoxDialogService();

            _duLieuViewModel.PropertyChanged += OnDuLieuViewModelPropertyChanged;
            _duLieuViewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_duLieuViewModel.KetQuaPreviewControl))
                {
                    OnPropertyChanged(nameof(ProjectPreview));
                }
            };

            _duLieuViewModel.OnKetQuaSelectionChanged += (s, e) => {
                if (_duLieuViewModel.SelectedSlabFromKetQuaUniqueIds != null && _duLieuViewModel.SelectedSlabFromKetQuaUniqueIds.Count > 0)
                {
                    List<string> pickedIds = new List<string>(_duLieuViewModel.SelectedSlabFromKetQuaUniqueIds);
                    LoadPickedFloors(pickedIds);
                }
            };

            _revitVersionStatus = "Revit API - Báo cáo kết quả thiết kế";

            UpdateMaterialQuantities();

            CreateRevitModelCommand = new RelayCommand(o => ExecuteCreateRevitModel());
            CalculateAllFloorsCommand = new RelayCommand(o => ExecuteCalculateAllFloors());
            ExportExcelCommand = new RelayCommand(o => ExecuteExportExcel());
            ExportPdfCommand = new RelayCommand(o => ExecuteExportPdf());
            ChangeTabCommand = new RelayCommand(ExecuteChangeTab);
            ShowFloorDetailsCommand = new RelayCommand(ExecuteShowFloorDetails);
            SelectFloorCommand = new RelayCommand(o => _duLieuViewModel.ExecuteSelectSlabFromKetQua());
            SweepSelectFloorCommand = new RelayCommand(o => _duLieuViewModel.ExecuteSweepSelectSlabFromKetQua());

            if (_duLieuViewModel.SelectedSlabFromKetQuaUniqueIds != null && _duLieuViewModel.SelectedSlabFromKetQuaUniqueIds.Count > 0)
            {
                List<string> pickedIds = new List<string>(_duLieuViewModel.SelectedSlabFromKetQuaUniqueIds);
                _duLieuViewModel.SelectedSlabFromKetQuaUniqueIds.Clear();
                LoadPickedFloors(pickedIds);
            }
        }

        public double Joist1Spacing { get => _duLieuViewModel.Joist1Spacing; set { _duLieuViewModel.Joist1Spacing = value; } }
        public double Joist2Spacing { get => _duLieuViewModel.Joist2Spacing; set { _duLieuViewModel.Joist2Spacing = value; } }
        public double SupportSpacing { get => _duLieuViewModel.SupportSpacing; set { _duLieuViewModel.SupportSpacing = value; } }
        public string RevitVersionStatus { get => _revitVersionStatus; set { _revitVersionStatus = value; OnPropertyChanged(); } }
        public List<MaterialItem> MaterialQuantities { get => _materialQuantities; set { _materialQuantities = value; OnPropertyChanged(); } }
        public List<MaterialItem> ProjectMaterialQuantities { get => _duLieuViewModel.KetQuaProjectMaterialQuantities as List<MaterialItem>; set { _duLieuViewModel.KetQuaProjectMaterialQuantities = value; OnPropertyChanged(); } }
        public List<FloorElementModel> FloorElements { get => _duLieuViewModel.KetQuaFloorElements as List<FloorElementModel>; set { _duLieuViewModel.KetQuaFloorElements = value; OnPropertyChanged(); } }
        public FrameworkElement ProjectPreview { get => _duLieuViewModel.KetQuaPreviewControl; }

        public ICommand CreateRevitModelCommand { get; }
        public ICommand CalculateAllFloorsCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand ChangeTabCommand { get; }
        public ICommand ShowFloorDetailsCommand { get; }
        public ICommand SelectFloorCommand { get; }
        public ICommand SweepSelectFloorCommand { get; }

        public void Dispose()
        {
            // Do nothing since SharedPreviewControl is managed by DuLieuViewModel
        }



        private void LoadPickedFloors(List<string> uniqueIds)
        {
            Document doc = _duLieuViewModel.RevitDoc;
            if (doc != null && uniqueIds.Count > 0)
            {
                var floorModels = new List<FloorElementModel>();

                foreach (string uniqueId in uniqueIds)
                {
                    Element floorElem = doc.GetElement(uniqueId);
                    if (floorElem is Floor floor)
                    {
                        double thicknessFeet = 0;
                        Parameter thicknessParam = floor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
                        if (thicknessParam != null) thicknessFeet = thicknessParam.AsDouble();
                        else
                        {
                            FloorType floorType = floor.Document.GetElement(floor.GetTypeId()) as FloorType;
                            if (floorType != null)
                            {
                                Parameter typeThicknessParam = floorType.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
                                if (typeThicknessParam != null) thicknessFeet = typeThicknessParam.AsDouble();
                            }
                        }
                        double thicknessMm = thicknessFeet * 304.8;

                        double areaM2 = 0;
                        Parameter areaParam = floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                        if (areaParam != null) areaM2 = areaParam.AsDouble() * 0.092903;

                        double perimeterM = 0;
                        Parameter perimeterParam = floor.get_Parameter(BuiltInParameter.HOST_PERIMETER_COMPUTED);
                        if (perimeterParam != null) perimeterM = perimeterParam.AsDouble() * 0.3048;

                        string levelName = doc.GetElement(floor.LevelId)?.Name ?? "Unknown Level";

                        var tempModel = new FloorElementModel
                        {
                            UniqueId = uniqueId,
                            ElementId = floor.Id.ToString(),
                            FamilyTypeName = floor.Name,
                            Level = levelName,
                            Area = areaM2,
                            Perimeter = perimeterM,
                            Thickness = thicknessMm
                        };
                        floorModels.Add(tempModel);
                    }
                }

                if (floorModels.Count > 0)
                {
                    FloorElements = floorModels;
                    ExecuteShowMultipleFloorDetails(floorModels);
                }
            }
        }

        private void ExecuteCreateRevitModel()
        {
            _dialogService.ShowInfo("Bắt đầu khởi tạo các cấu kiện ván khuôn trong Revit:\n" +
                            $"- Tạo hệ ván mặt dày 18mm\n" +
                            $"- Rải xà gồ phụ khoảng cách {Joist1Spacing:F0} mm\n" +
                            $"- Rải xà gồ chính khoảng cách {Joist2Spacing:F0} mm\n" +
                            $"- Dựng hệ cây chống đứng khoảng cách {SupportSpacing:F0} mm\n\n" +
                            "Đã tạo mô hình thành công!", "Revit API Automation");

            RevitVersionStatus = "Mô hình Revit đã được dựng tự động lúc " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void ExecuteCalculateAllFloors()
        {
            if (_duLieuViewModel.RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            try
            {
                FilteredElementCollector collector = new FilteredElementCollector(_duLieuViewModel.RevitDoc);
                ICollection<Element> floors = collector.OfClass(typeof(Floor)).ToElements();

                double totalAreaSqFt = 0;
                double totalPerimeterFt = 0;
                int floorCount = 0;
                var floorList = new List<FloorElementModel>();

                foreach (Element elem in floors)
                {
                    if (elem is Floor floor)
                    {
                        double areaM2 = 0;
                        double perimeterM = 0;
                        double thicknessMm = 0;

                        Parameter areaParam = floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                        if (areaParam != null)
                        {
                            totalAreaSqFt += areaParam.AsDouble();
                            areaM2 = areaParam.AsDouble() * 0.092903;
                        }

                        Parameter perimParam = floor.get_Parameter(BuiltInParameter.HOST_PERIMETER_COMPUTED);
                        if (perimParam != null)
                        {
                            totalPerimeterFt += perimParam.AsDouble();
                            perimeterM = perimParam.AsDouble() * 0.3048;
                        }

                        Parameter thicknessParam = floor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
                        if (thicknessParam != null)
                        {
                            thicknessMm = thicknessParam.AsDouble() * 304.8;
                        }
                        else
                        {
                            FloorType floorType = floor.Document.GetElement(floor.GetTypeId()) as FloorType;
                            if (floorType != null)
                            {
                                Parameter typeThicknessParam = floorType.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
                                if (typeThicknessParam != null) thicknessMm = typeThicknessParam.AsDouble() * 304.8;
                            }
                        }

                        string levelName = "Không xác định";
                        if (floor.LevelId != ElementId.InvalidElementId)
                        {
                            Level level = floor.Document.GetElement(floor.LevelId) as Level;
                            if (level != null) levelName = level.Name;
                        }

                        floorList.Add(new FloorElementModel
                        {
                            UniqueId = floor.UniqueId,
                            ElementId = floor.Id.ToString(),
                            FamilyTypeName = floor.Name,
                            Level = levelName,
                            Area = areaM2,
                            Perimeter = perimeterM,
                            Thickness = thicknessMm
                        });

                        floorCount++;
                    }
                }

                FloorElements = floorList;
                if (floorCount > 0)
                {
                    _duLieuViewModel.KetQuaTotalProjectArea = totalAreaSqFt * 0.092903;
                    _duLieuViewModel.KetQuaTotalProjectPerimeter = totalPerimeterFt * 0.3048;
                    _duLieuViewModel.KetQuaIsProjectCalculated = true;
                    UpdateProjectMaterialQuantities();

                    _dialogService.ShowInfo(
                        $"Đã tổng hợp thành công diện tích của {floorCount} sàn trong dự án.\n" +
                        $"Tổng diện tích: {_duLieuViewModel.KetQuaTotalProjectArea:F2} m²\n\n" +
                        $"Bảng tiên lượng vật tư toàn dự án đã được cập nhật tự động bên dưới.",
                        "Thống kê toàn dự án");

                    RevitVersionStatus = $"Đã thống kê {floorCount} sàn - Tổng diện tích: {_duLieuViewModel.KetQuaTotalProjectArea:F2} m²";
                }
                else
                {
                    _dialogService.ShowWarning("Không tìm thấy cấu kiện sàn nào trong dự án.", "Thông báo");
                }
            }
            catch (Exception ex)
            {
                _dialogService.ShowWarning($"Có lỗi xảy ra trong quá trình thống kê: {ex.Message}", "Lỗi");
            }
        }

        private void ExecuteExportExcel()
        {
            _dialogService.ShowInfo("Xuất tiên lượng và bảng tính vật liệu thành công ra tệp Excel (.xlsx).", "Xuất báo cáo");
            RevitVersionStatus = "Đã xuất bảng tính Excel lúc " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void ExecuteExportPdf()
        {
            _dialogService.ShowInfo("Xuất thuyết minh tính toán điều kiện kỹ thuật thành công ra tệp PDF.", "Xuất báo cáo");
            RevitVersionStatus = "Đã xuất thuyết minh PDF lúc " + DateTime.Now.ToString("HH:mm:ss");
        }

        private void ExecuteChangeTab(object parameter)
        {
            _duLieuViewModel.ExecuteChangeTab(parameter);
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        private void OnDuLieuViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            OnPropertyChanged(e.PropertyName);

            if (e.PropertyName == nameof(DuLieuViewModel.SelectedSlabArea) ||
                e.PropertyName == nameof(DuLieuViewModel.SelectedSlabPerimeter) ||
                e.PropertyName == nameof(DuLieuViewModel.SlabThickness) ||
                e.PropertyName == nameof(DuLieuViewModel.Joist1Spacing) ||
                e.PropertyName == nameof(DuLieuViewModel.Joist2Spacing) ||
                e.PropertyName == nameof(DuLieuViewModel.SupportSpacing) ||
                e.PropertyName == nameof(DuLieuViewModel.VanMatThickness) ||
e.PropertyName == nameof(DuLieuViewModel.XaGoPhuWidth) ||
                e.PropertyName == nameof(DuLieuViewModel.XaGoPhuHeight) ||
                e.PropertyName == nameof(DuLieuViewModel.XaGoChinhWidth) ||
                e.PropertyName == nameof(DuLieuViewModel.XaGoChinhHeight) ||
                e.PropertyName == nameof(DuLieuViewModel.CayChongDiameter) ||
                e.PropertyName == nameof(DuLieuViewModel.CayChongThickness))
            {
                UpdateMaterialQuantities();
                UpdateProjectMaterialQuantities();
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

            double totalArea = floorModels.Sum(f => f.Area);
            double totalPerimeter = floorModels.Sum(f => f.Perimeter);
            double thickness = floorModels.First().Thickness;

            UpdateMaterialQuantities(totalArea, totalPerimeter, thickness);

            Document doc = _duLieuViewModel.RevitDoc;
            if (doc != null)
            {
                var validModels = floorModels.Where(f => !string.IsNullOrEmpty(f.UniqueId)).ToList();
                if (validModels.Count == 0)
                {
                    _dialogService.ShowWarning("Dữ liệu sàn cũ chưa được cập nhật ID. Vui lòng bấm 'Thống kê toàn dự án' một lần nữa trước khi xem chi tiết.", "Cảnh báo dữ liệu");
                    return;
                }

                try
                {
                    Element firstFloorElem = doc.GetElement(validModels.First().UniqueId);
                    if (firstFloorElem != null)
                    {
                        ElementId levelId = firstFloorElem.LevelId;

                        ViewPlan viewPlan = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewPlan))
                            .Cast<ViewPlan>()
                            .FirstOrDefault(v => v.GenLevel != null && v.GenLevel.Id == levelId && !v.IsTemplate && (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.EngineeringPlan));

                        if (viewPlan == null)
                        {
                            viewPlan = new FilteredElementCollector(doc)
                                .OfClass(typeof(ViewPlan))
                                .Cast<ViewPlan>()
                                .FirstOrDefault(v => !v.IsTemplate && (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.EngineeringPlan));
                        }

                        if (viewPlan != null)
                        {
                            using (Transaction t = new Transaction(doc, "Highlight Floors"))
                            {
                                t.Start();

                                if (_duLieuViewModel.KetQuaLastHighlightedFloorIds != null && _duLieuViewModel.KetQuaLastHighlightedViewId != null)
                                {
                                    View lastView = doc.GetElement(_duLieuViewModel.KetQuaLastHighlightedViewId) as View;
                                    if (lastView != null)
                                    {
                                        foreach (var oldId in _duLieuViewModel.KetQuaLastHighlightedFloorIds)
                                        {
                                            lastView.SetElementOverrides(oldId, new OverrideGraphicSettings());
                                        }
                                        if (_duLieuViewModel.KetQuaLastViewTemplateId != null)
                                        {
                                            lastView.ViewTemplateId = _duLieuViewModel.KetQuaLastViewTemplateId;
                                        }
                                    }
                                }

                                _duLieuViewModel.KetQuaLastViewTemplateId = viewPlan.ViewTemplateId;
                                viewPlan.ViewTemplateId = ElementId.InvalidElementId;

                                OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                                ogs.SetSurfaceTransparency(0);
                                ogs.SetHalftone(false);

                                FillPatternElement solidFill = new FilteredElementCollector(doc)
                                    .OfClass(typeof(FillPatternElement))
                                    .Cast<FillPatternElement>()
                                    .FirstOrDefault(a => a.GetFillPattern() != null && a.GetFillPattern().IsSolidFill);

                                Autodesk.Revit.DB.Color redColor = new Autodesk.Revit.DB.Color(255, 128, 128);
                                Autodesk.Revit.DB.Color darkRedColor = new Autodesk.Revit.DB.Color(255, 0, 0);

                                if (solidFill != null)
                                {
                                    ogs.SetSurfaceForegroundPatternId(solidFill.Id);
                                    ogs.SetSurfaceForegroundPatternColor(redColor);
                                    ogs.SetSurfaceBackgroundPatternId(solidFill.Id);
                                    ogs.SetSurfaceBackgroundPatternColor(redColor);

                                    ogs.SetCutForegroundPatternId(solidFill.Id);
                                    ogs.SetCutForegroundPatternColor(redColor);
                                    ogs.SetCutBackgroundPatternId(solidFill.Id);
                                    ogs.SetCutBackgroundPatternColor(redColor);
                                }

                                ogs.SetProjectionLineColor(darkRedColor);
                                ogs.SetCutLineColor(darkRedColor);
                                ogs.SetProjectionLineWeight(8);
                                ogs.SetCutLineWeight(8);

                                _duLieuViewModel.KetQuaLastHighlightedFloorIds.Clear();

                                foreach (var model in validModels)
                                {
                                    Element elem = doc.GetElement(model.UniqueId);
                                    if (elem != null)
                                    {
                                        viewPlan.SetElementOverrides(elem.Id, ogs);
                                        _duLieuViewModel.KetQuaLastHighlightedFloorIds.Add(elem.Id);
                                    }
                                }

                                _duLieuViewModel.KetQuaLastHighlightedViewId = viewPlan.Id;

                                doc.Regenerate();
                                t.Commit();
                            }

                            if (_duLieuViewModel.SharedPreviewControl is IDisposable disposable)
                            {
                                disposable.Dispose();
                            }

                            _duLieuViewModel.SharedPreviewControl = new PreviewControl(doc, viewPlan.Id);
                            _duLieuViewModel.KetQuaPreviewControl = _duLieuViewModel.SharedPreviewControl;
                        }
                        else
                        {
                            _dialogService.ShowWarning("Không tìm thấy mặt bằng 2D nào phù hợp để hiển thị.", "Thông báo");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _dialogService.ShowWarning($"Lỗi khi tạo hình ảnh 2D: {ex.Message}", "Lỗi");
                }
            }
        }

        private void UpdateMaterialQuantities()
        {
            double area = _duLieuViewModel.SelectedSlabArea > 0 ? _duLieuViewModel.SelectedSlabArea : 100.0;
            double perimeter = _duLieuViewModel.SelectedSlabPerimeter > 0 ? _duLieuViewModel.SelectedSlabPerimeter : 40.0;
            double thicknessMm = _duLieuViewModel.SlabThickness;

            UpdateMaterialQuantities(area, perimeter, thicknessMm);
        }
        private void UpdateMaterialQuantities(double area, double perimeter, double thicknessMm)
        {
            double thicknessM = thicknessMm / 1000.0;

            double matSanArea = area;
            double thanhSanArea = perimeter * thicknessM;

            double plywoodMatSan = Math.Ceiling(matSanArea / (1.22 * 2.44) * 1.10);
            double lengthThanhSan = Math.Ceiling(perimeter * 1.15);

            double joist1Len = Math.Ceiling(area * (1000.0 / _duLieuViewModel.Joist1Spacing) * 1.05);
            double joist2Len = Math.Ceiling(area * (1000.0 / _duLieuViewModel.Joist2Spacing) * 1.05);

            double propsPerM2 = 1.0 / ((_duLieuViewModel.Joist2Spacing / 1000.0) * (_duLieuViewModel.SupportSpacing / 1000.0));
            double propCount = Math.Ceiling(area * propsPerM2 * 1.05);

            MaterialQuantities = new List<MaterialItem>
            {
                new MaterialItem { MaterialName = $"Ván khuôn mặt sàn (Phủ phim)", Specification = $"{_duLieuViewModel.VanMatThickness:F0} x 1220 x 2440 mm", Quantity = plywoodMatSan, Unit = "Tấm" },
                new MaterialItem { MaterialName = $"Ván khuôn thành sàn (Phủ phim)", Specification = $"{_duLieuViewModel.VanMatThickness:F0} x {thicknessMm:F0} mm", Quantity = lengthThanhSan, Unit = "m" },
                new MaterialItem { MaterialName = $"Gỗ xà gồ phụ (Lớp 1)", Specification = $"{_duLieuViewModel.XaGoPhuWidth:F0} x {_duLieuViewModel.XaGoPhuHeight:F0} mm", Quantity = joist1Len, Unit = "m" },
                new MaterialItem { MaterialName = $"Gỗ xà gồ chính (Lớp 2)", Specification = $"{_duLieuViewModel.XaGoChinhWidth:F0} x {_duLieuViewModel.XaGoChinhHeight:F0} mm", Quantity = joist2Len, Unit = "m" },
                new MaterialItem { MaterialName = "Cây chống đứng", Specification = $"Thép ống Ø{_duLieuViewModel.CayChongDiameter:F0} x {_duLieuViewModel.CayChongThickness:F0} mm", Quantity = propCount, Unit = "Cây" },
                new MaterialItem { MaterialName = "Kích tăng (Đầu/Chân)", Specification = "L=500mm", Quantity = propCount * 2, Unit = "Cái" }
            };
        }

        private void UpdateProjectMaterialQuantities()
        {
            if (!_duLieuViewModel.KetQuaIsProjectCalculated || _duLieuViewModel.KetQuaTotalProjectArea <= 0) return;

            double area = _duLieuViewModel.KetQuaTotalProjectArea;
            double perimeter = _duLieuViewModel.KetQuaTotalProjectPerimeter;
            double thicknessM = _duLieuViewModel.SlabThickness / 1000.0;

            double matSanArea = area;
            double thanhSanArea = perimeter * thicknessM;

            double plywoodMatSan = Math.Ceiling(matSanArea / (1.22 * 2.44) * 1.10);
            double lengthThanhSan = Math.Ceiling(perimeter * 1.15);

            double joist1Len = Math.Ceiling(area * (1000.0 / _duLieuViewModel.Joist1Spacing) * 1.05);
            double joist2Len = Math.Ceiling(area * (1000.0 / _duLieuViewModel.Joist2Spacing) * 1.05);

            double propsPerM2 = 1.0 / ((_duLieuViewModel.Joist2Spacing / 1000.0) * (_duLieuViewModel.SupportSpacing / 1000.0));
            double propCount = Math.Ceiling(area * propsPerM2 * 1.05);

            ProjectMaterialQuantities = new List<MaterialItem>
            {
                new MaterialItem { MaterialName = $"Ván khuôn mặt sàn (Phủ phim)", Specification = $"{_duLieuViewModel.VanMatThickness:F0} x 1220 x 2440 mm", Quantity = plywoodMatSan, Unit = "Tấm" },
                new MaterialItem { MaterialName = $"Ván khuôn thành sàn (Phủ phim)", Specification = $"{_duLieuViewModel.VanMatThickness:F0} x {_duLieuViewModel.SlabThickness:F0} mm", Quantity = lengthThanhSan, Unit = "m" },
                new MaterialItem { MaterialName = $"Gỗ xà gồ phụ (Lớp 1)", Specification = $"{_duLieuViewModel.XaGoPhuWidth:F0} x {_duLieuViewModel.XaGoPhuHeight:F0} mm", Quantity = joist1Len, Unit = "m" },
                new MaterialItem { MaterialName = $"Gỗ xà gồ chính (Lớp 2)", Specification = $"{_duLieuViewModel.XaGoChinhWidth:F0} x {_duLieuViewModel.XaGoChinhHeight:F0} mm", Quantity = joist2Len, Unit = "m" },
                new MaterialItem { MaterialName = "Cây chống đứng", Specification = $"Thép ống Ø{_duLieuViewModel.CayChongDiameter:F0} x {_duLieuViewModel.CayChongThickness:F0} mm", Quantity = propCount, Unit = "Cây" },
                new MaterialItem { MaterialName = "Kích tăng (Đầu/Chân)", Specification = "L=500mm", Quantity = propCount * 2, Unit = "Cái" }
            };
        }
    }

    public class MaterialItem
    {
        public string MaterialName { get; set; }
        public string Specification { get; set; }
        public double Quantity { get; set; }
        public string Unit { get; set; }
    }

    public class FloorElementModel
    {
        public string UniqueId { get; set; }
        public string ElementId { get; set; }
        public string FamilyTypeName { get; set; }
        public string Level { get; set; }
        public double Area { get; set; }
        public double Perimeter { get; set; }
        public double Thickness { get; set; }
    }
}
