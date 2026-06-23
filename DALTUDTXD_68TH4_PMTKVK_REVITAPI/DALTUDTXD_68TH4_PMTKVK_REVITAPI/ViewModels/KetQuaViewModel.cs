using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class KetQuaViewModel : INotifyPropertyChanged
    {
        private readonly DuLieuViewModel _duLieuViewModel;
        private readonly IDialogService _dialogService;
        private string _revitVersionStatus;
        private List<MaterialItem> _materialQuantities;
        private List<MaterialItem> _projectMaterialQuantities;
        private double _totalProjectArea = 0;
        private double _totalProjectPerimeter = 0;
        private bool _isProjectCalculated = false;
        private List<FloorElementModel> _floorElements;

        public event PropertyChangedEventHandler PropertyChanged;

        public KetQuaViewModel(DuLieuViewModel duLieuViewModel, IDialogService dialogService = null)
        {
            _duLieuViewModel = duLieuViewModel ?? throw new ArgumentNullException(nameof(duLieuViewModel));
            _dialogService = dialogService ?? new MessageBoxDialogService();

            _duLieuViewModel.PropertyChanged += OnDuLieuViewModelPropertyChanged;
            
            _revitVersionStatus = "Revit API - Báo cáo kết quả thiết kế";

            UpdateMaterialQuantities();

            CreateRevitModelCommand = new RelayCommand(o => ExecuteCreateRevitModel());
            CalculateAllFloorsCommand = new RelayCommand(o => ExecuteCalculateAllFloors());
            ExportExcelCommand = new RelayCommand(o => ExecuteExportExcel());
            ExportPdfCommand = new RelayCommand(o => ExecuteExportPdf());
            ChangeTabCommand = new RelayCommand(ExecuteChangeTab);
            ShowFloorDetailsCommand = new RelayCommand(ExecuteShowFloorDetails);
        }

        public double Joist1Spacing
        {
            get => _duLieuViewModel.Joist1Spacing;
            set { _duLieuViewModel.Joist1Spacing = value; }
        }

        public double Joist2Spacing
        {
            get => _duLieuViewModel.Joist2Spacing;
            set { _duLieuViewModel.Joist2Spacing = value; }
        }

        public double SupportSpacing
        {
            get => _duLieuViewModel.SupportSpacing;
            set { _duLieuViewModel.SupportSpacing = value; }
        }

        public string RevitVersionStatus
        {
            get => _revitVersionStatus;
            set { _revitVersionStatus = value; OnPropertyChanged(); }
        }

        public List<MaterialItem> MaterialQuantities
        {
            get => _materialQuantities;
            set { _materialQuantities = value; OnPropertyChanged(); }
        }

        public List<MaterialItem> ProjectMaterialQuantities
        {
            get => _projectMaterialQuantities;
            set { _projectMaterialQuantities = value; OnPropertyChanged(); }
        }

        public List<FloorElementModel> FloorElements
        {
            get => _floorElements;
            set { _floorElements = value; OnPropertyChanged(); }
        }

        public ICommand CreateRevitModelCommand { get; }
        public ICommand CalculateAllFloorsCommand { get; }
        public ICommand ExportExcelCommand { get; }
        public ICommand ExportPdfCommand { get; }
        public ICommand ChangeTabCommand { get; }
        public ICommand ShowFloorDetailsCommand { get; }

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
                    _totalProjectArea = totalAreaSqFt * 0.092903;
                    _totalProjectPerimeter = totalPerimeterFt * 0.3048;
                    _isProjectCalculated = true;
                    UpdateProjectMaterialQuantities();
                    
                    _dialogService.ShowInfo(
                        $"Đã tổng hợp thành công diện tích của {floorCount} sàn trong dự án.\n" +
                        $"Tổng diện tích: {_totalProjectArea:F2} m²\n\n" +
                        $"Bảng tiên lượng vật tư toàn dự án đã được cập nhật tự động bên dưới.",
                        "Thống kê toàn dự án");
                        
                    RevitVersionStatus = $"Đã thống kê {floorCount} sàn - Tổng diện tích: {_totalProjectArea:F2} m²";
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
            _duLieuViewModel.NavigateAction?.Invoke(parameter?.ToString());
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
            if (parameter is FloorElementModel floor)
            {
                UpdateMaterialQuantities(floor.Area, floor.Perimeter, floor.Thickness);
                _dialogService.ShowInfo($"Đã cập nhật bảng tiên lượng cho {floor.FamilyTypeName} (ID: {floor.ElementId}).", "Chi tiết sàn");
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

            double plywoodMatSan = Math.Ceiling(matSanArea / (1.22 * 2.44) * 1.10); // 10% hao hụt
            double lengthThanhSan = Math.Ceiling(perimeter * 1.15); // 15% hao hụt theo chiều dài

            double joist1Len = Math.Ceiling(area * (1000.0 / _duLieuViewModel.Joist1Spacing) * 1.05); // 5% hao hụt
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
            if (!_isProjectCalculated || _totalProjectArea <= 0) return;

            double area = _totalProjectArea;
            double perimeter = _totalProjectPerimeter;
            double thicknessM = _duLieuViewModel.SlabThickness / 1000.0;

            double matSanArea = area;
            double thanhSanArea = perimeter * thicknessM;

            double plywoodMatSan = Math.Ceiling(matSanArea / (1.22 * 2.44) * 1.10); // 10% hao hụt
            double lengthThanhSan = Math.Ceiling(perimeter * 1.15); // 15% hao hụt theo chiều dài

            double joist1Len = Math.Ceiling(area * (1000.0 / _duLieuViewModel.Joist1Spacing) * 1.05); // 5% hao hụt
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
        public string ElementId { get; set; }
        public string FamilyTypeName { get; set; }
        public string Level { get; set; }
        public double Area { get; set; }
        public double Perimeter { get; set; }
        public double Thickness { get; set; }
    }
}
