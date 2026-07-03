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
        
        public event PropertyChangedEventHandler PropertyChanged;

        public KetQuaViewModel(SharedDataViewModel SharedDataViewModel, IDialogService dialogService = null)
        {
            _SharedDataViewModel = SharedDataViewModel ?? throw new ArgumentNullException(nameof(SharedDataViewModel));
            _dialogService = dialogService ?? new MessageBoxDialogService();

            _SharedDataViewModel.PropertyChanged += OnSharedDataViewModelPropertyChanged;
            _SharedDataViewModel.SlabSelectionCompleted += OnSlabSelectionCompleted;
            
            _revitVersionStatus = "Revit API - Báo cáo kết quả thiết kế";

            UpdateMaterialQuantities();

            CreateRevitModelCommand = new RelayCommand(o => ExecuteCreateRevitModel());
            CalculateAllFloorsCommand = new RelayCommand(o => ExecuteCalculateAllFloors());
            ExportExcelCommand = new RelayCommand(o => ExecuteExportExcel());
            ExportPdfCommand = new RelayCommand(o => ExecuteExportPdf());
            ExportWordCommand = new RelayCommand(o => ExecuteExportWord());
            ChangeTabCommand = new RelayCommand(ExecuteChangeTab);
            ShowFloorDetailsCommand = new RelayCommand(ExecuteShowFloorDetails);
            SelectFloorCommand = new RelayCommand(o => _SharedDataViewModel.ExecuteSelectSlabFromKetQua());
            SweepSelectFloorCommand = new RelayCommand(o => _SharedDataViewModel.ExecuteSweepSelectSlabFromKetQua());

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
            if (_SharedDataViewModel.RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            try
            {
                FilteredElementCollector collector = new FilteredElementCollector(_SharedDataViewModel.RevitDoc);
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
                    _SharedDataViewModel.KetQuaTotalProjectArea = totalAreaSqFt * 0.092903;
                    _SharedDataViewModel.KetQuaTotalProjectPerimeter = totalPerimeterFt * 0.3048;
                    _SharedDataViewModel.KetQuaIsProjectCalculated = true;
                    UpdateProjectMaterialQuantities();
                    
                    _dialogService.ShowInfo(
                        $"Đã tổng hợp thành công diện tích của {floorCount} sàn trong dự án.\n" +
                        $"Tổng diện tích: {_SharedDataViewModel.KetQuaTotalProjectArea:F2} m²\n\n" +
                        $"Bảng tiên lượng vật tư toàn dự án đã được cập nhật tự động bên dưới.",
                        "Thống kê toàn dự án");
                        
                    RevitVersionStatus = $"Đã thống kê {floorCount} sàn - Tổng diện tích: {_SharedDataViewModel.KetQuaTotalProjectArea:F2} m²";
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
                            var ws = workbook.Worksheets.Add("Sàn Đang Chọn");
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
                        SoNhipVanMat = _SharedDataViewModel.SoNhipVanMat,
                        SoNhipXaGoPhu = _SharedDataViewModel.SoNhipXaGoPhu,
                        SoNhipXaGoChinh = _SharedDataViewModel.SoNhipXaGoChinh,
                        Joist1Spacing = _SharedDataViewModel.Joist1Spacing > 0 ? _SharedDataViewModel.Joist1Spacing : 400.0,
                        Joist2Spacing = _SharedDataViewModel.Joist2Spacing > 0 ? _SharedDataViewModel.Joist2Spacing : 1000.0,
                        SupportSpacing = _SharedDataViewModel.SupportSpacing > 0 ? _SharedDataViewModel.SupportSpacing : 1000.0
                    };

                    var calcResult = FormworkCalculationService.CheckSafety(p);

                    // Thu thập List<KetQuaTinhToan> từ kết quả
                    var listKetQua = new List<KetQuaTinhToan>();
                    if (calcResult.Plywood.StressResult != null) listKetQua.Add(calcResult.Plywood.StressResult);
                    if (calcResult.Plywood.DeflectionResult != null) listKetQua.Add(calcResult.Plywood.DeflectionResult);
                    if (calcResult.Joist1.StressResult != null) listKetQua.Add(calcResult.Joist1.StressResult);
                    if (calcResult.Joist1.ShearResult != null) listKetQua.Add(calcResult.Joist1.ShearResult);
                    if (calcResult.Joist1.DeflectionResult != null) listKetQua.Add(calcResult.Joist1.DeflectionResult);
                    if (calcResult.Joist2.StressResult != null) listKetQua.Add(calcResult.Joist2.StressResult);
                    if (calcResult.Joist2.ShearResult != null) listKetQua.Add(calcResult.Joist2.ShearResult);
                    if (calcResult.Joist2.DeflectionResult != null) listKetQua.Add(calcResult.Joist2.DeflectionResult);
                    if (calcResult.Support.StressResult != null) listKetQua.Add(calcResult.Support.StressResult);

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
            OnPropertyChanged(e.PropertyName);

            if (e.PropertyName == nameof(SharedDataViewModel.KetQuaPreviewControl))
            {
                OnPropertyChanged(nameof(ProjectPreview));
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
                e.PropertyName == nameof(SharedDataViewModel.CayChongThickness))
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

            _SharedDataViewModel.SelectedSlabArea = totalArea;
            _SharedDataViewModel.SelectedSlabPerimeter = totalPerimeter;
            
            UpdateMaterialQuantities();
            
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
            double area = _SharedDataViewModel.SelectedSlabArea > 0 ? _SharedDataViewModel.SelectedSlabArea : 100.0;
            double perimeter = _SharedDataViewModel.SelectedSlabPerimeter > 0 ? _SharedDataViewModel.SelectedSlabPerimeter : 40.0;
            double thicknessMm = _SharedDataViewModel.SlabThickness;

            MaterialQuantities = CalculateQuantities(area, perimeter, thicknessMm);
        }

        private void UpdateProjectMaterialQuantities()
        {
            if (!_SharedDataViewModel.KetQuaIsProjectCalculated || _SharedDataViewModel.KetQuaTotalProjectArea <= 0) return;

            double area = _SharedDataViewModel.KetQuaTotalProjectArea;
            double perimeter = _SharedDataViewModel.KetQuaTotalProjectPerimeter;
            double thicknessMm = _SharedDataViewModel.SlabThickness;

            ProjectMaterialQuantities = CalculateQuantities(area, perimeter, thicknessMm);
        }

        private List<MaterialItem> CalculateQuantities(double area, double perimeter, double thicknessMm)
        {
            double thicknessM = thicknessMm / 1000.0;

            double matSanArea = area;
            double thanhSanArea = perimeter * thicknessM;

            double plywoodMatSan = Math.Ceiling(matSanArea / (1.22 * 2.44) * 1.10); 
            double lengthThanhSan = Math.Ceiling(perimeter * 1.15); 

            double joist1Len = Math.Ceiling(area * (1000.0 / _SharedDataViewModel.Joist1Spacing) * 1.05); 
            double joist2Len = Math.Ceiling(area * (1000.0 / _SharedDataViewModel.Joist2Spacing) * 1.05);
            
            double propsPerM2 = 1.0 / ((_SharedDataViewModel.Joist2Spacing / 1000.0) * (_SharedDataViewModel.SupportSpacing / 1000.0));
            double propCount = Math.Ceiling(area * propsPerM2 * 1.05);

            return new List<MaterialItem>
            {
                new MaterialItem { MaterialName = $"Ván khuôn mặt sàn (Phủ phim)", Specification = $"{_SharedDataViewModel.VanMatThickness:F0} x 1220 x 2440 mm", Quantity = plywoodMatSan, Unit = "Tấm" },
                new MaterialItem { MaterialName = $"Ván khuôn thành sàn (Phủ phim)", Specification = $"{_SharedDataViewModel.VanMatThickness:F0} x {thicknessMm:F0} mm", Quantity = lengthThanhSan, Unit = "m" },
                new MaterialItem { MaterialName = $"Gỗ xà gồ phụ (Lớp 1)", Specification = $"{_SharedDataViewModel.XaGoPhuWidth:F0} x {_SharedDataViewModel.XaGoPhuHeight:F0} mm", Quantity = joist1Len, Unit = "m" },
                new MaterialItem { MaterialName = $"Gỗ xà gồ chính (Lớp 2)", Specification = $"{_SharedDataViewModel.XaGoChinhWidth:F0} x {_SharedDataViewModel.XaGoChinhHeight:F0} mm", Quantity = joist2Len, Unit = "m" },
                new MaterialItem { MaterialName = "Cây chống đứng", Specification = $"Thép ống Ø{_SharedDataViewModel.CayChongDiameter:F0} x {_SharedDataViewModel.CayChongThickness:F0} mm", Quantity = propCount, Unit = "Cây" },
                new MaterialItem { MaterialName = "Kích tăng (Đầu/Chân)", Specification = "L=500mm", Quantity = propCount * 2, Unit = "Cái" }
            };
        }
    }

}
