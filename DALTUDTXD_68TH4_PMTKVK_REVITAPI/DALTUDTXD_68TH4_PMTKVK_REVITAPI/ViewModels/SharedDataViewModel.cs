using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Services;
using DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    /// <summary>
    /// Payload truyền qua sự kiện SlabSelectionCompleted, chứa toàn bộ dữ liệu
    /// sau khi người dùng chọn sàn từ Revit, để các trang đồng bộ cùng lúc.
    /// </summary>
    public class SlabSelectionEventArgs : EventArgs
    {
        public List<FloorElementModel> FloorElements { get; set; }
        public double SlabThickness { get; set; }
        public double SelectedSlabArea { get; set; }
        public double SelectedSlabPerimeter { get; set; }
        public double Joist1Spacing { get; set; }
        public double Joist2Spacing { get; set; }
        public double SupportSpacing { get; set; }
        public int FloorCount { get; set; }
        public string MaxFloorName { get; set; }
        public List<List<System.Windows.Point>> SlabPolygons { get; set; }
    }

    public class SharedDataViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private string _revitVersionStatus;
        private List<KetQuaHienThi> _checkResults;
        public Action<string> NavigateAction { get; private set; }
        private readonly Action _anSoCuaSo;
        private readonly IDialogService _dialogService;

        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// Sự kiện phát ra sau khi hoàn tất chọn sàn từ Revit.
        /// Tất cả các ViewModel con đều subscribe để đồng bộ dữ liệu.
        /// </summary>
        public event EventHandler<SlabSelectionEventArgs> SlabSelectionCompleted;

        /// <summary>
        /// Sự kiện phát ra khi người dùng yêu cầu dựng mô hình 3D từ trang Kết quả.
        /// Preview3DViewModel subscribe để bắt đầu render pipeline.
        /// </summary>
        public event EventHandler Render3DRequested;

        /// <summary>
        /// Phát sự kiện yêu cầu dựng 3D, đồng thời chuyển sang tab Preview3DView.
        /// </summary>
        public void RequestRender3D()
        {
            Render3DRequested?.Invoke(this, EventArgs.Empty);
            ExecuteChangeTab("Preview3DView");
        }

        public Document RevitDoc { get; private set; }
        public UIDocument RevitUIDoc { get; private set; }

        // Dữ liệu dùng chung
        private double _woodStrength = 15.0;
        public double WoodStrength { get => _woodStrength; set { _woodStrength = value; OnPropertyChanged(); } }

        private double _plywoodBendingStrength = 10.0;
        public double PlywoodBendingStrength { get => _plywoodBendingStrength; set { _plywoodBendingStrength = value; OnPropertyChanged(); } }

        private double _woodShearStrength = 2.0;
        public double WoodShearStrength { get => _woodShearStrength; set { _woodShearStrength = value; OnPropertyChanged(); } }

        private double _elasticModulus = 9000.0;
        public double ElasticModulus { get => _elasticModulus; set { _elasticModulus = value; OnPropertyChanged(); } }

        private string _slabDeflectionLimit = "1/250";
        public string SlabDeflectionLimit { get => _slabDeflectionLimit; set { _slabDeflectionLimit = value; OnPropertyChanged(); } }

        private string _joistDeflectionLimit = "1/250";
        public string JoistDeflectionLimit { get => _joistDeflectionLimit; set { _joistDeflectionLimit = value; OnPropertyChanged(); } }

        private double _woodFormworkWeight = 0.30;
        public double WoodFormworkWeight { get => _woodFormworkWeight; set { _woodFormworkWeight = value; OnPropertyChanged(); } }

        private double _concreteWeight = 25.00;
        public double ConcreteWeight { get => _concreteWeight; set { _concreteWeight = value; OnPropertyChanged(); } }

        private double _constructionLiveLoad = 2.50;
        public double ConstructionLiveLoad { get => _constructionLiveLoad; set { _constructionLiveLoad = value; OnPropertyChanged(); } }

        private double _dynamicImpactFactor = 1.30;
        public double DynamicImpactFactor { get => _dynamicImpactFactor; set { _dynamicImpactFactor = value; OnPropertyChanged(); } }

        private double _deadLoadSafetyFactor = 1.20;
        public double DeadLoadSafetyFactor { get => _deadLoadSafetyFactor; set { _deadLoadSafetyFactor = value; OnPropertyChanged(); } }

        private double _liveLoadSafetyFactor = 1.30;
        public double LiveLoadSafetyFactor { get => _liveLoadSafetyFactor; set { _liveLoadSafetyFactor = value; OnPropertyChanged(); } }

        private double _moistureFactor = 0.85;
        public double MoistureFactor { get => _moistureFactor; set { _moistureFactor = value; OnPropertyChanged(); } }

        private double _temperatureFactor = 1.00;
        public double TemperatureFactor { get => _temperatureFactor; set { _temperatureFactor = value; OnPropertyChanged(); } }

        private double _joist1Spacing = 400.0;
        public double Joist1Spacing { get => _joist1Spacing; set { _joist1Spacing = value; OnPropertyChanged(); } }

        private double _joist2Spacing = 1000.0;
        public double Joist2Spacing { get => _joist2Spacing; set { _joist2Spacing = value; OnPropertyChanged(); } }

        private double _supportSpacing = 1000.0;
        public double SupportSpacing { get => _supportSpacing; set { _supportSpacing = value; OnPropertyChanged(); } }

        private double _slabThickness = 150.0;
        public double SlabThickness { get => _slabThickness; set { _slabThickness = value; OnPropertyChanged(); } }

        private double _vanMatThickness = 18.0;
        public double VanMatThickness { get => _vanMatThickness; set { _vanMatThickness = value; OnPropertyChanged(); } }

        private double _xaGoPhuWidth = 50.0;
        public double XaGoPhuWidth { get => _xaGoPhuWidth; set { _xaGoPhuWidth = value; OnPropertyChanged(); } }

        private double _xaGoPhuHeight = 100.0;
        public double XaGoPhuHeight { get => _xaGoPhuHeight; set { _xaGoPhuHeight = value; OnPropertyChanged(); } }

        private double _xaGoChinhWidth = 100.0;
        public double XaGoChinhWidth { get => _xaGoChinhWidth; set { _xaGoChinhWidth = value; OnPropertyChanged(); } }

        private double _xaGoChinhHeight = 100.0;
        public double XaGoChinhHeight { get => _xaGoChinhHeight; set { _xaGoChinhHeight = value; OnPropertyChanged(); } }

        private double _cayChongDiameter = 48.0;
        public double CayChongDiameter { get => _cayChongDiameter; set { _cayChongDiameter = value; OnPropertyChanged(); } }

        private double _cayChongThickness = 2.0;
        public double CayChongThickness { get => _cayChongThickness; set { _cayChongThickness = value; OnPropertyChanged(); } }

        private double _cayChongHeight = 3600.0;
        public double CayChongHeight { get => _cayChongHeight; set { _cayChongHeight = value; OnPropertyChanged(); } }

        private int _soNhipVanMat = 2;
        public int SoNhipVanMat { get => _soNhipVanMat; set { _soNhipVanMat = value; OnPropertyChanged(); } }

        private int _soNhipXaGoPhu = 3;
        public int SoNhipXaGoPhu { get => _soNhipXaGoPhu; set { _soNhipXaGoPhu = value; OnPropertyChanged(); } }

        private int _soNhipXaGoChinh = 3;
        public int SoNhipXaGoChinh { get => _soNhipXaGoChinh; set { _soNhipXaGoChinh = value; OnPropertyChanged(); } }

        private int _selectedTieuChuan = 0;
        public int SelectedTieuChuan { get => _selectedTieuChuan; set { _selectedTieuChuan = value; OnPropertyChanged(); } }

        private bool _isSelectingSlab = false;
        public bool IsSelectingSlab { get => _isSelectingSlab; set { _isSelectingSlab = value; OnPropertyChanged(); } }
        
        private bool _isSweepSelectingSlab = false;
        public bool IsSweepSelectingSlab { get => _isSweepSelectingSlab; set { _isSweepSelectingSlab = value; OnPropertyChanged(); } }

        /// <summary>
        /// Stores the latest selection payload so newly constructed ViewModels can instantly sync state.
        /// </summary>
        public SlabSelectionEventArgs LatestSelectionContext { get; private set; }
        public List<Tuple<string, double>> SelectedSlabInfos { get; set; } = new List<Tuple<string, double>>();
        
        private List<FloorElementModel> _floorElements;
        public List<FloorElementModel> FloorElements { get => _floorElements; set { _floorElements = value; OnPropertyChanged(); } }

        private string _selectedFloorDetailName;
        public string SelectedFloorDetailName { get => _selectedFloorDetailName; set { _selectedFloorDetailName = value; OnPropertyChanged(); } }
        
        public string StartupTab { get; set; }

        public object KetQuaFloorElements { get; set; }
        public double KetQuaTotalProjectArea { get; set; }
        public double KetQuaTotalProjectPerimeter { get; set; }
        public bool KetQuaIsProjectCalculated { get; set; }
        public object KetQuaProjectMaterialQuantities { get; set; }
        
        public List<ElementId> KetQuaLastHighlightedFloorIds { get; set; } = new List<ElementId>();
        public ElementId KetQuaLastHighlightedViewId { get; set; }
        public ElementId KetQuaLastViewTemplateId { get; set; }

        private bool _isCalculated = false;
        public bool IsCalculated { get => _isCalculated; set { _isCalculated = value; OnPropertyChanged(); } }

        private double _selectedSlabArea = 0.0;
        public double SelectedSlabArea { get => _selectedSlabArea; set { _selectedSlabArea = value; OnPropertyChanged(); } }

        private double _selectedSlabPerimeter = 0.0;
        public double SelectedSlabPerimeter { get => _selectedSlabPerimeter; set { _selectedSlabPerimeter = value; OnPropertyChanged(); } }

        private System.Windows.FrameworkElement _sharedPreviewControl;
        public System.Windows.FrameworkElement SharedPreviewControl { get => _sharedPreviewControl; set { _sharedPreviewControl = value; OnPropertyChanged(); } }

        private System.Windows.FrameworkElement _duLieuPreviewControl;
        public System.Windows.FrameworkElement DuLieuPreviewControl { get => _duLieuPreviewControl; set { _duLieuPreviewControl = value; OnPropertyChanged(); } }

        private System.Windows.FrameworkElement _tinhToanPreviewControl;
        public System.Windows.FrameworkElement TinhToanPreviewControl { get => _tinhToanPreviewControl; set { _tinhToanPreviewControl = value; OnPropertyChanged(); } }

        private System.Windows.FrameworkElement _ketQuaPreviewControl;
        public System.Windows.FrameworkElement KetQuaPreviewControl { get => _ketQuaPreviewControl; set { _ketQuaPreviewControl = value; OnPropertyChanged(); } }

        public SharedDataViewModel(IDialogService dialogService, Action anSoCuaSo = null)
        {
            _dialogService = dialogService ?? new MessageBoxDialogService();
            _anSoCuaSo = anSoCuaSo;

            LoadDefaults();

            UpdateResultsCommand = new RelayCommand(o => ExecuteRunAnalysis());
            RunAnalysisCommand = new RelayCommand(o => ExecuteRunAnalysis());
            ChangeTabCommand = new RelayCommand(ExecuteChangeTab);
            SelectSlabCommand = new RelayCommand(o => ExecuteSelectSlab());
            SweepSelectSlabCommand = new RelayCommand(o => ExecuteSweepSelectSlab());
            ShowFloorDetailsCommand = new RelayCommand(ExecuteShowFloorDetails);

            ExecuteUpdateResults(false);
        }

        public void LoadDefaults()
        {
            var p = DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models.CalculationParameters.Default;
            WoodStrength = p.WoodStrength;
            ElasticModulus = p.ElasticModulus;
            ConcreteWeight = p.ConcreteWeight;
            WoodFormworkWeight = p.WoodFormworkWeight;
            ConstructionLiveLoad = p.ConstructionLiveLoad;
            DeadLoadSafetyFactor = p.DeadLoadSafetyFactor;
            LiveLoadSafetyFactor = p.LiveLoadSafetyFactor;
            MoistureFactor = p.MoistureFactor;
            
            // Deflection limit ratio conversion back to string (simplified for typical values)
            SlabDeflectionLimit = "1/400";
            JoistDeflectionLimit = "1/400";

            VanMatThickness = 18.0;
            XaGoPhuWidth = 50.0;
            XaGoPhuHeight = 100.0;
            XaGoChinhWidth = 100.0;
            XaGoChinhHeight = 100.0;
            CayChongDiameter = 48.0;
            CayChongThickness = 2.0;
            CayChongHeight = 3600.0;

            Joist1Spacing = 400.0;
            Joist2Spacing = 1000.0;
            SupportSpacing = 1000.0;
            
            SoNhipVanMat = 2;
            SoNhipXaGoPhu = 3;
            SoNhipXaGoChinh = 3;
        }

        public void ExecuteChangeTab(object parameter)
        {
            string tabName = parameter?.ToString();
            
            if ((tabName == "DuLieuView" || tabName == "TinhToanView" || tabName == "Report" || tabName == "KetQuaView") && SharedPreviewControl == null && RevitDoc != null)
            {
                InitializePreviewControl(RevitDoc);
            }

            // Chuyển quyền sở hữu PreviewControl cho tab đang active để tránh lỗi WPF
            if (tabName == "TinhToanView" || tabName == "Report")
            {
                KetQuaPreviewControl = null;
                DuLieuPreviewControl = null;
                TinhToanPreviewControl = SharedPreviewControl;
            }
            else if (tabName == "KetQuaView")
            {
                TinhToanPreviewControl = null;
                DuLieuPreviewControl = null;
                KetQuaPreviewControl = SharedPreviewControl;
            }
            else if (tabName == "DuLieuView")
            {
                TinhToanPreviewControl = null;
                KetQuaPreviewControl = null;
                DuLieuPreviewControl = SharedPreviewControl;
            }
            else
            {
                TinhToanPreviewControl = null;
                KetQuaPreviewControl = null;
                DuLieuPreviewControl = null;
            }

            NavigateAction?.Invoke(tabName);
        }

        public void InitializeRevitContext(Document doc, UIDocument uiDoc, Action<string> navigateAction)
        {
            RevitDoc = doc;
            RevitUIDoc = uiDoc;
            NavigateAction = navigateAction;

            if (RevitDoc != null)
            {
                RevitVersionStatus = $"Revit API {RevitDoc.Application.VersionNumber} - Dự án: {RevitDoc.Title}";
            }
            else
            {
                RevitVersionStatus = "Revit API - Chế độ thiết kế (Standalone/Offline)";
            }
        }

        public void InitializePreviewControl(Document doc)
        {
            if (SharedPreviewControl == null && doc != null)
            {
                try
                {
                    ViewPlan viewPlan = null;
                    if (KetQuaLastHighlightedViewId != null)
                    {
                        viewPlan = doc.GetElement(KetQuaLastHighlightedViewId) as ViewPlan;
                    }
                    
                    if (viewPlan == null)
                    {
                        viewPlan = new FilteredElementCollector(doc)
                            .OfClass(typeof(ViewPlan))
                            .Cast<ViewPlan>()
                            .FirstOrDefault(v => !v.IsTemplate && (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.EngineeringPlan));
                    }

                    if (viewPlan != null)
                    {
                        SharedPreviewControl = new PreviewControl(doc, viewPlan.Id);
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Lỗi khởi tạo Shared Preview: {ex.Message}");
                }
            }
        }

        public void DisposePreviewControl()
        {
            if (SharedPreviewControl is IDisposable disposable)
            {
                disposable.Dispose();
            }
            SharedPreviewControl = null;
            KetQuaPreviewControl = null;
            TinhToanPreviewControl = null;
        }

        public string RevitVersionStatus
        {
            get => _revitVersionStatus;
            set { _revitVersionStatus = value; OnPropertyChanged(); }
        }

        public List<KetQuaHienThi> CheckResults
        {
            get => _checkResults;
            set { _checkResults = value; OnPropertyChanged(); }
        }

        public ICommand UpdateResultsCommand { get; }
        public ICommand RunAnalysisCommand { get; }
        public ICommand ChangeTabCommand { get; }
        public ICommand SelectSlabCommand { get; }
        public ICommand SweepSelectSlabCommand { get; }
        public ICommand ShowFloorDetailsCommand { get; }

        private void ExecuteUpdateResults(bool showWarning = false)
        {
            if (!string.IsNullOrEmpty(Error))
            {
                if (showWarning)
                {
                    _dialogService.ShowWarning("Vui lòng kiểm tra lại các thông số đầu vào không hợp lệ.", "Cảnh báo");
                }
                return;
            }

            var p = new Models.CalculationParameters
            {
                WoodStrength = WoodStrength > 0 ? WoodStrength : 15.0,
                PlywoodBendingStrength = PlywoodBendingStrength > 0 ? PlywoodBendingStrength : 10.0,
                WoodShearStrength = WoodShearStrength > 0 ? WoodShearStrength : 2.0,
                ElasticModulus = ElasticModulus,
                ConcreteWeight = ConcreteWeight > 0 ? ConcreteWeight : 25.0,
                WoodFormworkWeight = WoodFormworkWeight,
                ConstructionLiveLoad = ConstructionLiveLoad,
                DynamicImpactFactor = DynamicImpactFactor > 0 ? DynamicImpactFactor : 1.3,
                DeadLoadSafetyFactor = DeadLoadSafetyFactor,
                LiveLoadSafetyFactor = LiveLoadSafetyFactor,
                MoistureFactor = MoistureFactor,
                TemperatureFactor = TemperatureFactor,
                SlabDeflectionLimitRatio = Models.FormworkCalculationService.ParseFraction(SlabDeflectionLimit),
                JoistDeflectionLimitRatio = Models.FormworkCalculationService.ParseFraction(JoistDeflectionLimit),
                SlabThickness = SlabThickness,
                VanMatThickness = VanMatThickness,
                XaGoPhuWidth = XaGoPhuWidth,
                XaGoPhuHeight = XaGoPhuHeight,
                XaGoChinhWidth = XaGoChinhWidth,
                XaGoChinhHeight = XaGoChinhHeight,
                CayChongDiameter = CayChongDiameter,
                CayChongThickness = CayChongThickness,
                CayChongHeight = CayChongHeight,
                SoNhipVanMat = SoNhipVanMat,
                SoNhipXaGoPhu = SoNhipXaGoPhu,
                SoNhipXaGoChinh = SoNhipXaGoChinh,
                Joist1Spacing = Joist1Spacing > 0 ? Joist1Spacing : 400.0,
                Joist2Spacing = Joist2Spacing > 0 ? Joist2Spacing : 1000.0,
                SupportSpacing = SupportSpacing > 0 ? SupportSpacing : 1000.0,
                SelectedFloorDetailName = SelectedFloorDetailName
            };

            var res = Models.FormworkCalculationService.CheckSafety(p);
            CheckResults = res.ReportItems;
        }

        private void ExecuteRunAnalysis()
        {
            ExecuteUpdateResults(true);
            IsCalculated = true;
            RevitVersionStatus = "Đã hoàn thành phân tích kết cấu lúc " + DateTime.Now.ToString("HH:mm:ss");
        }


        private void ExecuteSelectSlab()
        {
            if (RevitUIDoc == null || RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            IsSelectingSlab = true;
            StartupTab = "DuLieuView";
            _anSoCuaSo?.Invoke();
        }

        private void ExecuteSweepSelectSlab()
        {
            if (RevitUIDoc == null || RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            IsSweepSelectingSlab = true;
            StartupTab = "DuLieuView";
            _anSoCuaSo?.Invoke();
        }

        private void ExecuteShowFloorDetails(object parameter)
        {
            if (parameter is FloorElementModel floorModel)
            {
                if (floorModel.Thickness > 0)
                {
                    SlabThickness = floorModel.Thickness;
                    SelectedFloorDetailName = floorModel.FamilyTypeName;
                    ExecuteUpdateResults(false);
                }
            }
        }

        public void ExecuteSelectSlabFromKetQua()
        {
            if (RevitUIDoc == null || RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            IsSelectingSlab = true;
            StartupTab = "KetQuaView";
            _anSoCuaSo?.Invoke();
        }

        public void ExecuteSweepSelectSlabFromKetQua()
        {
            if (RevitUIDoc == null || RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            IsSweepSelectingSlab = true;
            StartupTab = "KetQuaView";
            _anSoCuaSo?.Invoke();
        }

        public void ExecuteSelectSlabFromTinhToan()
        {
            if (RevitUIDoc == null || RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            IsSelectingSlab = true;
            StartupTab = "TinhToanView";
            _anSoCuaSo?.Invoke();
        }

        public void ExecuteSweepSelectSlabFromTinhToan()
        {
            if (RevitUIDoc == null || RevitDoc == null)
            {
                _dialogService.ShowWarning("Chức năng này chỉ khả dụng khi chạy trực tiếp trong Revit.", "Thông báo");
                return;
            }

            IsSweepSelectingSlab = true;
            StartupTab = "TinhToanView";
            _anSoCuaSo?.Invoke();
        }

        public void ProcessSelectedSlab(System.Collections.Generic.IList<Reference> references)
        {
            if (references != null && references.Count > 0)
            {
                double totalArea = 0;
                double totalPerimeter = 0;
                double maxThicknessMm = 0;
                string maxFloorName = "";

                SelectedSlabInfos.Clear();
                
                var floorModels = new List<FloorElementModel>();
                var allPolygons = new List<List<System.Windows.Point>>();

                foreach (var refer in references)
                {
                    Element element = RevitDoc.GetElement(refer.ElementId);
                    if (element is Floor floor)
                    {
                        double thicknessFeet = 0;
                        Parameter thicknessParam = floor.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
                        if (thicknessParam != null)
                        {
                            thicknessFeet = thicknessParam.AsDouble();
                        }
                        else
                        {
                            FloorType floorType = floor.Document.GetElement(floor.GetTypeId()) as FloorType;
                            if (floorType != null)
                            {
                                Parameter typeThicknessParam = floorType.get_Parameter(BuiltInParameter.FLOOR_ATTR_THICKNESS_PARAM);
                                if (typeThicknessParam != null)
                                    thicknessFeet = typeThicknessParam.AsDouble();
                            }
                        }

                        // Extract Geometry
                        Options opt = new Options();
                        opt.ComputeReferences = true;
                        GeometryElement geomElem = floor.get_Geometry(opt);
                        if (geomElem != null)
                        {
                            foreach (GeometryObject geomObj in geomElem)
                            {
                                if (geomObj is Solid solid && solid.Faces.Size > 0)
                                {
                                    Face topFace = null;
                                    foreach (Face face in solid.Faces)
                                    {
                                        if (face is PlanarFace pf && pf.FaceNormal.IsAlmostEqualTo(XYZ.BasisZ))
                                        {
                                            topFace = face;
                                            break;
                                        }
                                    }

                                    if (topFace != null)
                                    {
                                        foreach (EdgeArray edgeArray in topFace.EdgeLoops)
                                        {
                                            var polygon = new List<System.Windows.Point>();
                                            foreach (Edge edge in edgeArray)
                                            {
                                                var pts = edge.Tessellate();
                                                for (int i = 0; i < pts.Count - 1; i++) // exclude last point to avoid dupes at corners
                                                {
                                                    polygon.Add(new System.Windows.Point(pts[i].X * 304.8, pts[i].Y * 304.8));
                                                }
                                            }
                                            allPolygons.Add(polygon);
                                        }
                                    }
                                }
                            }
                        }
                        
                        double thicknessMm = thicknessFeet * 304.8;
                        SelectedSlabInfos.Add(new Tuple<string, double>(floor.Name, thicknessMm));

                        if (thicknessMm > maxThicknessMm)
                        {
                            maxThicknessMm = thicknessMm;
                            maxFloorName = floor.Name;
                        }

                        double areaM2 = 0;
                        Parameter areaParam = floor.get_Parameter(BuiltInParameter.HOST_AREA_COMPUTED);
                        if (areaParam != null)
                        {
                            double areaSqFt = areaParam.AsDouble();
                            areaM2 = areaSqFt * 0.092903; // convert sq ft to m2
                            totalArea += areaM2; 
                        }

                        Parameter perimeterParam = floor.get_Parameter(BuiltInParameter.HOST_PERIMETER_COMPUTED);
                        if (perimeterParam != null)
                        {
                            double perimeterFt = perimeterParam.AsDouble();
                            totalPerimeter += perimeterFt * 0.3048; // convert ft to m
                        }
                        
                        string levelName = RevitDoc.GetElement(floor.LevelId)?.Name ?? "Unknown Level";

                        var tempModel = new FloorElementModel
                        {
                            UniqueId = floor.UniqueId,
                            ElementId = floor.Id.ToString(),
                            FamilyTypeName = floor.Name,
                            Level = levelName,
                            Area = areaM2,
                            Thickness = thicknessMm
                        };
                        floorModels.Add(tempModel);
                    }
                }

                if (floorModels.Count > 0)
                {
                    FloorElements = floorModels;

                    // Normalize polygons to (0,0)
                    if (allPolygons.Count > 0)
                    {
                        double minX = double.MaxValue, minY = double.MaxValue;
                        foreach (var poly in allPolygons)
                        {
                            foreach (var pt in poly)
                            {
                                if (pt.X < minX) minX = pt.X;
                                if (pt.Y < minY) minY = pt.Y;
                            }
                        }
                        for (int i = 0; i < allPolygons.Count; i++)
                        {
                            for (int j = 0; j < allPolygons[i].Count; j++)
                            {
                                allPolygons[i][j] = new System.Windows.Point(allPolygons[i][j].X - minX, allPolygons[i][j].Y - minY);
                            }
                        }
                    }

                    // Chọn sàn dày nhất (nguy hiểm nhất) để hiện chi tiết mặc định
                    var maxFloor = floorModels.OrderByDescending(f => f.Thickness).FirstOrDefault();
                    if (maxFloor != null)
                    {
                        SelectedFloorDetailName = maxFloor.FamilyTypeName;
                        SlabThickness = maxFloor.Thickness;
                    }
                    else
                    {
                        SlabThickness = maxThicknessMm;
                    }
                    SelectedSlabArea = totalArea > 0 ? totalArea : 100.0;
                    SelectedSlabPerimeter = totalPerimeter > 0 ? totalPerimeter : 40.0;

                    AutoDesignFormwork();

                    RevitVersionStatus = $"Đã chọn {floorModels.Count} sàn | Chiều dày nguy hiểm nhất: {maxThicknessMm:F0} mm";

                    _dialogService.ShowInfo(
                        $"Đã chọn thành công {floorModels.Count} sàn từ Revit!\n" +
                        $"- Sàn nguy hiểm nhất (dày nhất): {maxFloorName}\n" +
                        $"- Chiều dày: {maxThicknessMm:F0} mm\n" +
                        $"- Tổng diện tích: {SelectedSlabArea:F2} m²\n\n" +
                        $"- Tự động tính toán lại các khoảng cách xà gồ, cây chống tối ưu.",
                        "Revit API Integration");
                }
                
                // Highlight the selected slabs
                var selectedIds = floorModels.Select(f => new ElementId(long.Parse(f.ElementId))).ToList();
                UpdatePreviewGraphics(selectedIds);

                // Lưu trạng thái trước khi phát sự kiện
                LatestSelectionContext = new SlabSelectionEventArgs
                {
                    FloorElements = floorModels,
                    SlabThickness = SlabThickness,
                    SelectedSlabArea = SelectedSlabArea,
                    SelectedSlabPerimeter = SelectedSlabPerimeter,
                    Joist1Spacing = Joist1Spacing,
                    Joist2Spacing = Joist2Spacing,
                    SupportSpacing = SupportSpacing,
                    FloorCount = floorModels.Count,
                    MaxFloorName = maxFloorName,
                    SlabPolygons = allPolygons
                };

                // Phát sự kiện đồng bộ toàn bộ trang sau khi chọn sàn xong
                SlabSelectionCompleted?.Invoke(this, LatestSelectionContext);

                // Đánh dấu IsCalculated để TinhToan tự động recalculate
                IsCalculated = true;
            }
        }

        private void UpdatePreviewGraphics(List<ElementId> selectedFloorIds)
        {
            if (RevitDoc == null || RevitUIDoc == null) return;
            
            try
            {
                View activeView = RevitUIDoc.ActiveView;
                if (activeView == null) return;

                using (Transaction t = new Transaction(RevitDoc, "Highlight Slabs"))
                {
                    t.Start();
                    
                    if (KetQuaLastHighlightedFloorIds != null && KetQuaLastHighlightedViewId != null)
                    {
                        View lastView = RevitDoc.GetElement(KetQuaLastHighlightedViewId) as View;
                        if (lastView != null)
                        {
                            foreach (var oldId in KetQuaLastHighlightedFloorIds)
                            {
                                lastView.SetElementOverrides(oldId, new OverrideGraphicSettings());
                            }
                        }
                    }
                    
                    OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                    ogs.SetSurfaceTransparency(0);
                    ogs.SetHalftone(false);
                    
                    FillPatternElement solidFill = new FilteredElementCollector(RevitDoc)
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
                    
                    KetQuaLastHighlightedFloorIds.Clear();
                    
                    foreach (var id in selectedFloorIds)
                    {
                        activeView.SetElementOverrides(id, ogs);
                        KetQuaLastHighlightedFloorIds.Add(id);
                    }
                    
                    KetQuaLastHighlightedViewId = activeView.Id;
                    
                    RevitDoc.Regenerate();
                    t.Commit();
                }
                
                // Cập nhật lại PreviewControl nếu cần
                if (SharedPreviewControl is IDisposable disposable)
                {
                    disposable.Dispose();
                }
                SharedPreviewControl = new Autodesk.Revit.UI.PreviewControl(RevitDoc, activeView.Id);
                
                // Gán lại cho tab hiện tại đang active
                if (DuLieuPreviewControl != null) DuLieuPreviewControl = SharedPreviewControl;
                else if (TinhToanPreviewControl != null) TinhToanPreviewControl = SharedPreviewControl;
                else if (KetQuaPreviewControl != null) KetQuaPreviewControl = SharedPreviewControl;
                else DuLieuPreviewControl = SharedPreviewControl; // Default
            }
            catch { /* Ignore */ }
        }

        public void RenderPreviewGraphics(List<string> validUniqueIds, Action<System.Windows.FrameworkElement> setPreviewControlAction)
        {
            if (RevitDoc == null || validUniqueIds == null || validUniqueIds.Count == 0) return;

            try
            {
                Element firstFloorElem = RevitDoc.GetElement(validUniqueIds.First());
                if (firstFloorElem != null)
                {
                    ElementId levelId = firstFloorElem.LevelId;
                    
                    ViewPlan viewPlan = new FilteredElementCollector(RevitDoc)
                        .OfClass(typeof(ViewPlan))
                        .Cast<ViewPlan>()
                        .FirstOrDefault(v => v.GenLevel != null && v.GenLevel.Id == levelId && !v.IsTemplate && (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.EngineeringPlan));

                    if (viewPlan == null)
                    {
                        viewPlan = new FilteredElementCollector(RevitDoc)
                            .OfClass(typeof(ViewPlan))
                            .Cast<ViewPlan>()
                            .FirstOrDefault(v => !v.IsTemplate && (v.ViewType == ViewType.FloorPlan || v.ViewType == ViewType.EngineeringPlan));
                    }

                    if (viewPlan != null)
                    {
                        using (Transaction t = new Transaction(RevitDoc, "Highlight Floors"))
                        {
                            t.Start();
                            
                            if (KetQuaLastHighlightedFloorIds != null && KetQuaLastHighlightedViewId != null)
                            {
                                View lastView = RevitDoc.GetElement(KetQuaLastHighlightedViewId) as View;
                                if (lastView != null)
                                {
                                    foreach (var oldId in KetQuaLastHighlightedFloorIds)
                                    {
                                        lastView.SetElementOverrides(oldId, new OverrideGraphicSettings());
                                    }
                                    if (KetQuaLastViewTemplateId != null)
                                    {
                                        lastView.ViewTemplateId = KetQuaLastViewTemplateId;
                                    }
                                }
                            }
                            
                            KetQuaLastViewTemplateId = viewPlan.ViewTemplateId;
                            viewPlan.ViewTemplateId = ElementId.InvalidElementId;

                            OverrideGraphicSettings ogs = new OverrideGraphicSettings();
                            ogs.SetSurfaceTransparency(0);
                            ogs.SetHalftone(false);
                            
                            FillPatternElement solidFill = new FilteredElementCollector(RevitDoc)
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
                            
                            KetQuaLastHighlightedFloorIds.Clear();
                            
                            foreach (var id in validUniqueIds)
                            {
                                Element elem = RevitDoc.GetElement(id);
                                if (elem != null)
                                {
                                    viewPlan.SetElementOverrides(elem.Id, ogs);
                                    KetQuaLastHighlightedFloorIds.Add(elem.Id);
                                }
                            }
                            
                            KetQuaLastHighlightedViewId = viewPlan.Id;
                            
                            RevitDoc.Regenerate();
                            t.Commit();
                        }
                            
                        if (SharedPreviewControl is IDisposable disposable)
                        {
                            disposable.Dispose();
                        }
                            
                        SharedPreviewControl = new PreviewControl(RevitDoc, viewPlan.Id);
                        setPreviewControlAction?.Invoke(SharedPreviewControl);
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


        private void AutoDesignFormwork()
        {
            var p = new Models.CalculationParameters
            {
                WoodStrength = WoodStrength > 0 ? WoodStrength : 15.0,
                PlywoodBendingStrength = PlywoodBendingStrength > 0 ? PlywoodBendingStrength : 10.0,
                WoodShearStrength = WoodShearStrength > 0 ? WoodShearStrength : 2.0,
                ElasticModulus = ElasticModulus,
                ConcreteWeight = ConcreteWeight > 0 ? ConcreteWeight : 25.0,
                WoodFormworkWeight = WoodFormworkWeight,
                ConstructionLiveLoad = ConstructionLiveLoad,
                DynamicImpactFactor = DynamicImpactFactor > 0 ? DynamicImpactFactor : 1.3,
                DeadLoadSafetyFactor = DeadLoadSafetyFactor,
                LiveLoadSafetyFactor = LiveLoadSafetyFactor,
                MoistureFactor = MoistureFactor,
                TemperatureFactor = TemperatureFactor,
                SlabDeflectionLimitRatio = Models.FormworkCalculationService.ParseFraction(SlabDeflectionLimit),
                JoistDeflectionLimitRatio = Models.FormworkCalculationService.ParseFraction(JoistDeflectionLimit),
                SlabThickness = SlabThickness,
                VanMatThickness = VanMatThickness,
                XaGoPhuWidth = XaGoPhuWidth,
                XaGoPhuHeight = XaGoPhuHeight,
                XaGoChinhWidth = XaGoChinhWidth,
                XaGoChinhHeight = XaGoChinhHeight,
                CayChongDiameter = CayChongDiameter,
                CayChongThickness = CayChongThickness,
                CayChongHeight = CayChongHeight,
                SoNhipVanMat = SoNhipVanMat,
                SoNhipXaGoPhu = SoNhipXaGoPhu,
                SoNhipXaGoChinh = SoNhipXaGoChinh
            };

            var res = Models.FormworkCalculationService.AutoDesign(p);
            Joist1Spacing = res.L1;
            Joist2Spacing = res.L2;
            SupportSpacing = res.L3;

            ExecuteUpdateResults(false);
        }

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));

            // Tự động cập nhật kết quả khi các thông số thay đổi
            if (name == nameof(WoodStrength) || name == nameof(ElasticModulus) ||
                name == nameof(SlabDeflectionLimit) || name == nameof(JoistDeflectionLimit) ||
                name == nameof(WoodFormworkWeight) || name == nameof(ConcreteWeight) ||
                name == nameof(XaGoChinhWidth) || name == nameof(XaGoChinhHeight) ||
                name == nameof(CayChongDiameter) || name == nameof(CayChongThickness) || name == nameof(CayChongHeight) ||
                name == nameof(SoNhipVanMat) || name == nameof(SoNhipXaGoPhu) || name == nameof(SoNhipXaGoChinh) ||
                name == nameof(Joist1Spacing) || name == nameof(Joist2Spacing) || name == nameof(SupportSpacing) ||
                name == nameof(SlabThickness) || name == nameof(VanMatThickness) ||
                name == nameof(XaGoPhuWidth) || name == nameof(XaGoPhuHeight) ||
                name == nameof(XaGoChinhWidth) || name == nameof(XaGoChinhHeight) ||
                name == nameof(CayChongDiameter) || name == nameof(CayChongThickness))
            {
                ExecuteUpdateResults(false);
            }
        }

        #region IDataErrorInfo Implementation

        public string Error => null;

        public string this[string columnName]
        {
            get
            {
                string error = null;
                switch (columnName)
                {
                    case nameof(WoodStrength):
                    case nameof(ElasticModulus):
                    case nameof(WoodFormworkWeight):
                    case nameof(ConcreteWeight):
                    case nameof(ConstructionLiveLoad):
                    case nameof(DeadLoadSafetyFactor):
                    case nameof(LiveLoadSafetyFactor):
                    case nameof(MoistureFactor):
                    case nameof(TemperatureFactor):
                    case nameof(Joist1Spacing):
                    case nameof(Joist2Spacing):
                    case nameof(SupportSpacing):
                    case nameof(VanMatThickness):
                    case nameof(XaGoPhuWidth):
                    case nameof(XaGoPhuHeight):
                    case nameof(XaGoChinhWidth):
                    case nameof(XaGoChinhHeight):
                    case nameof(CayChongDiameter):
                    case nameof(CayChongThickness):
                        var valueObj = GetType().GetProperty(columnName)?.GetValue(this);
                        if (valueObj is double val && val <= 0)
                        {
                            error = "Giá trị phải là một số thực dương.";
                        }
                        break;
                    case nameof(SlabDeflectionLimit):
                    case nameof(JoistDeflectionLimit):
                        string valLimit = (string)GetType().GetProperty(columnName)?.GetValue(this);
                        if (Models.FormworkCalculationService.ParseFraction(valLimit) <= 0)
                        {
                            error = "Độ võng giới hạn không hợp lệ (ví dụ hợp lệ: 1/250 hoặc 0.004).";
                        }
                        break;
                }
                return error;
            }
        }

        #endregion
    }
}
