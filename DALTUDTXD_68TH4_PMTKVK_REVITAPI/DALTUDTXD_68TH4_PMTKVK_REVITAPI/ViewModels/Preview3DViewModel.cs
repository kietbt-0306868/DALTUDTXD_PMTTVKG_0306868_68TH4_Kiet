using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using HelixToolkit;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.ViewModels
{
    public class Preview3DViewModel : INotifyPropertyChanged, IDisposable
    {
        private SharedDataViewModel _sharedViewModel;

        private Model3DGroup _modelGroup;
        public Model3DGroup ModelGroup
        {
            get => _modelGroup;
            set { _modelGroup = value; OnPropertyChanged(); }
        }

        private bool _isRendering;
        public bool IsRendering
        {
            get => _isRendering;
            set { _isRendering = value; OnPropertyChanged(); }
        }

        public Preview3DViewModel(SharedDataViewModel sharedViewModel)
        {
            _sharedViewModel = sharedViewModel;
            _sharedViewModel.PropertyChanged += SharedViewModel_PropertyChanged;
            _sharedViewModel.SlabSelectionCompleted += OnSlabSelectionCompleted;
            _sharedViewModel.Render3DRequested += OnRender3DRequested;
            
            // Initial generation
            Generate3DModelAsync();

            if (_sharedViewModel.LatestSelectionContext != null)
            {
                OnSlabSelectionCompleted(this, _sharedViewModel.LatestSelectionContext);
            }
        }

        private void OnSlabSelectionCompleted(object sender, SlabSelectionEventArgs e)
        {
            Generate3DModelAsync();
        }

        private void OnRender3DRequested(object sender, EventArgs e)
        {
            Generate3DModelAsync();
        }

        private void SharedViewModel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Re-render when calculation is completed or slab is changed
            if (e.PropertyName == nameof(SharedDataViewModel.IsCalculated) && _sharedViewModel.IsCalculated)
            {
                Generate3DModelAsync();
            }
        }

        public void Generate3DModelAsync()
        {
            if (IsRendering) return;
            IsRendering = true;

            try
            {
                // Capture data from UI thread safely before jumping to background thread
                double areaM2 = _sharedViewModel.SelectedSlabArea > 0 ? _sharedViewModel.SelectedSlabArea : 100.0;
                double slabThickness = _sharedViewModel.SlabThickness > 0 ? _sharedViewModel.SlabThickness : 150.0;
                double plywoodThickness = _sharedViewModel.VanMatThickness > 0 ? _sharedViewModel.VanMatThickness : 18.0;
                
                double secJoistW = _sharedViewModel.XaGoPhuWidth > 0 ? _sharedViewModel.XaGoPhuWidth : 50.0;
                double secJoistH = _sharedViewModel.XaGoPhuHeight > 0 ? _sharedViewModel.XaGoPhuHeight : 100.0;
                double L1 = _sharedViewModel.Joist1Spacing > 0 ? _sharedViewModel.Joist1Spacing : 400.0;
                
                double priJoistW = _sharedViewModel.XaGoChinhWidth > 0 ? _sharedViewModel.XaGoChinhWidth : 100.0;
                double priJoistH = _sharedViewModel.XaGoChinhHeight > 0 ? _sharedViewModel.XaGoChinhHeight : 100.0;
                double L2 = _sharedViewModel.Joist2Spacing > 0 ? _sharedViewModel.Joist2Spacing : 1000.0;
                
                double shoreDia = _sharedViewModel.CayChongDiameter > 0 ? _sharedViewModel.CayChongDiameter : 48.0;
                double shoreHeight = _sharedViewModel.CayChongHeight > 0 ? _sharedViewModel.CayChongHeight : 3600.0;
                double L3 = _sharedViewModel.SupportSpacing > 0 ? _sharedViewModel.SupportSpacing : 1000.0;

                var polygons = _sharedViewModel.LatestSelectionContext?.SlabPolygons;

                // Construct 3D elements on the UI thread to avoid DependencyObject threading violations
                Model3DGroup newModelGroup = Build3DModel(
                    areaM2, slabThickness, plywoodThickness,
                    secJoistW, secJoistH, L1,
                    priJoistW, priJoistH, L2,
                    shoreDia, shoreHeight, L3, polygons
                );

                ModelGroup = newModelGroup;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error generating 3D model: {ex.Message}");
            }
            finally
            {
                IsRendering = false;
            }
        }

        private Model3DGroup Build3DModel(
            double areaM2, double slabThickness, double plywoodThickness,
            double secJoistW, double secJoistH, double L1,
            double priJoistW, double priJoistH, double L2,
            double shoreDia, double shoreHeight, double L3,
            List<List<System.Windows.Point>> polygons = null)
        {
            var group = new Model3DGroup();

            // Materials
            var slabMaterial = MaterialHelper.CreateMaterial(Color.FromArgb(100, 200, 220, 240)); // Semi-transparent concrete
            var plywoodMaterial = MaterialHelper.CreateMaterial(Color.FromRgb(210, 180, 140)); // Tan wood
            var secJoistMaterial = MaterialHelper.CreateMaterial(Color.FromRgb(180, 150, 100)); // Darker wood
            var priJoistMaterial = MaterialHelper.CreateMaterial(Color.FromRgb(150, 120, 80)); // Even darker wood
            var shoreMaterial = MaterialHelper.CreateMaterial(Color.FromRgb(80, 130, 180)); // Steel blue
            var bracingMaterial = MaterialHelper.CreateMaterial(Color.FromRgb(255, 200, 0)); // Warning yellow
            var jackMaterial = MaterialHelper.CreateMaterial(Color.FromRgb(192, 192, 192)); // Silver/Steel

            // 0. Update bounds to match actual slab polygon if available
            double viewLengthX = Math.Min(Math.Sqrt(areaM2) * 1000.0, 10000.0);
            double viewLengthY = viewLengthX;

            if (polygons != null && polygons.Count > 0)
            {
                double maxX = 0, maxY = 0;
                foreach (var poly in polygons)
                {
                    foreach (var pt in poly)
                    {
                        if (pt.X > maxX) maxX = pt.X;
                        if (pt.Y > maxY) maxY = pt.Y;
                    }
                }
                if (maxX > 0) viewLengthX = maxX;
                if (maxY > 0) viewLengthY = maxY;
            }

            // Z Elevations
            double zJackBottom = 0;
            double jackHeight = 150.0; // Base jack height
            double zShoreBottom = zJackBottom + jackHeight;
            double zShoreTop = zShoreBottom + shoreHeight;
            double uHeadHeight = 150.0; // U-head height
            double zPriJoistBottom = zShoreTop + uHeadHeight;
            double zSecJoistBottom = zPriJoistBottom + priJoistH;
            double zPlywoodBottom = zSecJoistBottom + secJoistH;
            double zConcreteBottom = zPlywoodBottom + plywoodThickness;
            double zConcreteTop = zConcreteBottom + slabThickness;

            // 1. Slab & Plywood
            var slabBuilder = new SimpleMeshBuilder();
            var plywoodBuilder = new SimpleMeshBuilder();
            if (polygons != null && polygons.Count > 0)
            {
                double minY = double.MaxValue, maxY = double.MinValue;
                foreach (var poly in polygons)
                {
                    foreach (var pt in poly)
                    {
                        if (pt.Y < minY) minY = pt.Y;
                        if (pt.Y > maxY) maxY = pt.Y;
                    }
                }
                
                double stripWidth = 50.0; // 50mm strips for robust arbitrary shape approximation
                for (double y = minY; y < maxY; y += stripWidth)
                {
                    double currentStripWidth = Math.Min(stripWidth, maxY - y);
                    double midY = y + currentStripWidth / 2;
                    var xInts = GetXIntersections(midY, polygons);
                    
                    for (int i = 0; i < xInts.Count - 1; i += 2)
                    {
                        double xStart = xInts[i];
                        double xEnd = xInts[i + 1];
                        double length = xEnd - xStart;
                        if (length > 1) 
                        {
                            slabBuilder.AddBox(new Point3D(xStart + length / 2, y + currentStripWidth / 2, zConcreteBottom + slabThickness / 2), length, currentStripWidth, slabThickness);
                            plywoodBuilder.AddBox(new Point3D(xStart + length / 2, y + currentStripWidth / 2, zPlywoodBottom + plywoodThickness / 2), length, currentStripWidth, plywoodThickness);
                        }
                    }
                }
            }
            else
            {
                slabBuilder.AddBox(new Point3D(viewLengthX / 2, viewLengthY / 2, zConcreteBottom + slabThickness / 2), viewLengthX, viewLengthY, slabThickness);
                plywoodBuilder.AddBox(new Point3D(viewLengthX / 2, viewLengthY / 2, zPlywoodBottom + plywoodThickness / 2), viewLengthX, viewLengthY, plywoodThickness);
            }
            group.Children.Add(new GeometryModel3D(slabBuilder.ToMesh(), slabMaterial));
            group.Children.Add(new GeometryModel3D(plywoodBuilder.ToMesh(), plywoodMaterial));

            // 1.5. Side Formwork (Ván khuôn thành sàn)
            var sideFormBuilder = new SimpleMeshBuilder();
            double sideFormThick = plywoodThickness;
            if (polygons != null && polygons.Count > 0)
            {
                foreach (var poly in polygons)
                {
                    for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                    {
                        System.Windows.Point p1 = poly[j];
                        System.Windows.Point p2 = poly[i];
                        sideFormBuilder.AddWall(new Point3D(p1.X, p1.Y, 0), new Point3D(p2.X, p2.Y, 0), sideFormThick, slabThickness + sideFormThick, zConcreteBottom - sideFormThick);
                    }
                }
            }
            else
            {
                // Left/Right
                sideFormBuilder.AddBox(new Point3D(-sideFormThick / 2, viewLengthY / 2, zConcreteBottom + slabThickness / 2), sideFormThick, viewLengthY + 2 * sideFormThick, slabThickness + sideFormThick);
                sideFormBuilder.AddBox(new Point3D(viewLengthX + sideFormThick / 2, viewLengthY / 2, zConcreteBottom + slabThickness / 2), sideFormThick, viewLengthY + 2 * sideFormThick, slabThickness + sideFormThick);
                // Front/Back
                sideFormBuilder.AddBox(new Point3D(viewLengthX / 2, -sideFormThick / 2, zConcreteBottom + slabThickness / 2), viewLengthX, sideFormThick, slabThickness + sideFormThick);
                sideFormBuilder.AddBox(new Point3D(viewLengthX / 2, viewLengthY + sideFormThick / 2, zConcreteBottom + slabThickness / 2), viewLengthX, sideFormThick, slabThickness + sideFormThick);
            }
            group.Children.Add(new GeometryModel3D(sideFormBuilder.ToMesh(), plywoodMaterial));

            // 3. Secondary Joists (Run along Y axis, spaced along X axis by L1)
            var secJoistBuilder = new SimpleMeshBuilder();
            List<double> secJoistPositions = new List<double>();
            for (double x = secJoistW / 2; x <= viewLengthX - secJoistW / 2; x += L1)
            {
                secJoistPositions.Add(x);
            }
            if (secJoistPositions.Count == 0 || viewLengthX - secJoistW / 2 - secJoistPositions[secJoistPositions.Count - 1] > 10)
            {
                secJoistPositions.Add(viewLengthX - secJoistW / 2);
            }
            foreach (double xPos in secJoistPositions)
            {
                if (polygons != null && polygons.Count > 0)
                {
                    var yInts = GetYIntersections(xPos, polygons);
                    for (int i = 0; i < yInts.Count - 1; i += 2)
                    {
                        double yStart = yInts[i];
                        double yEnd = yInts[i + 1];
                        double length = yEnd - yStart;
                        if (length > 10) // Ignore tiny slivers
                        {
                            secJoistBuilder.AddBox(new Point3D(xPos, yStart + length / 2, zSecJoistBottom + secJoistH / 2), secJoistW, length, secJoistH);
                        }
                    }
                }
                else
                {
                    secJoistBuilder.AddBox(new Point3D(xPos, viewLengthY / 2, zSecJoistBottom + secJoistH / 2), secJoistW, viewLengthY, secJoistH);
                }
            }
            group.Children.Add(new GeometryModel3D(secJoistBuilder.ToMesh(), secJoistMaterial));

            // 4. Primary Joists (Run along X axis, spaced along Y axis by L2)
            var priJoistBuilder = new SimpleMeshBuilder();
            List<double> priJoistPositions = new List<double>();
            for (double y = priJoistW / 2; y <= viewLengthY - priJoistW / 2; y += L2)
            {
                priJoistPositions.Add(y);
            }
            if (priJoistPositions.Count == 0 || viewLengthY - priJoistW / 2 - priJoistPositions[priJoistPositions.Count - 1] > 10)
            {
                priJoistPositions.Add(viewLengthY - priJoistW / 2);
            }
            foreach (double yPos in priJoistPositions)
            {
                if (polygons != null && polygons.Count > 0)
                {
                    var xInts = GetXIntersections(yPos, polygons);
                    for (int i = 0; i < xInts.Count - 1; i += 2)
                    {
                        double xStart = xInts[i];
                        double xEnd = xInts[i + 1];
                        double length = xEnd - xStart;
                        if (length > 10)
                        {
                            priJoistBuilder.AddBox(new Point3D(xStart + length / 2, yPos, zPriJoistBottom + priJoistH / 2), length, priJoistW, priJoistH);
                        }
                    }
                }
                else
                {
                    priJoistBuilder.AddBox(new Point3D(viewLengthX / 2, yPos, zPriJoistBottom + priJoistH / 2), viewLengthX, priJoistW, priJoistH);
                }
            }
            group.Children.Add(new GeometryModel3D(priJoistBuilder.ToMesh(), priJoistMaterial));

            // 5. Vertical Shores, Base Jacks, and U-Heads (Spaced by L3 along X axis under Primary Joists)
            var shoreBuilder = new SimpleMeshBuilder();
            var bracingBuilder = new SimpleMeshBuilder();
            var jackBuilder = new SimpleMeshBuilder();
            
            List<double> shoreXPositions = new List<double>();
            for (double x = shoreDia / 2; x <= viewLengthX - shoreDia / 2; x += L3)
            {
                shoreXPositions.Add(x);
            }
            if (shoreXPositions.Count == 0 || viewLengthX - shoreDia / 2 - shoreXPositions[shoreXPositions.Count - 1] > 10)
            {
                shoreXPositions.Add(viewLengthX - shoreDia / 2);
            }

            bool[,] validShores = new bool[shoreXPositions.Count, priJoistPositions.Count];

            for (int j = 0; j < priJoistPositions.Count; j++)
            {
                double yPos = priJoistPositions[j];
                
                for (int i = 0; i < shoreXPositions.Count; i++)
                {
                    double xPos = shoreXPositions[i];
                    
                    if (polygons != null && polygons.Count > 0 && !IsPointInsideOrNear(new System.Windows.Point(xPos, yPos), polygons, 300.0))
                    {
                        continue;
                    }

                    validShores[i, j] = true;

                    // Base Jack (Base plate and threaded rod)
                    jackBuilder.AddBox(new Point3D(xPos, yPos, zJackBottom + 5), 150, 150, 10); // Base plate
                    jackBuilder.AddCylinder(new Point3D(xPos, yPos, zJackBottom + 10), new Point3D(xPos, yPos, zShoreBottom), 38, 12); // Threaded rod

                    // Vertical Tube
                    shoreBuilder.AddCylinder(new Point3D(xPos, yPos, zShoreBottom), new Point3D(xPos, yPos, zShoreTop), shoreDia, 16);

                    // U-Head (Realistic height)
                    double uHeadHeightVis = 100.0;
                    jackBuilder.AddCylinder(new Point3D(xPos, yPos, zShoreTop), new Point3D(xPos, yPos, zPriJoistBottom - 5), 38, 12); // Threaded rod
                    jackBuilder.AddBox(new Point3D(xPos, yPos, zPriJoistBottom - 5), 100, priJoistW + 10, 10); // U-head base plate
                    jackBuilder.AddBox(new Point3D(xPos, yPos - priJoistW / 2 - 5, zPriJoistBottom + uHeadHeightVis / 2), 100, 10, uHeadHeightVis); // U-head left side
                    jackBuilder.AddBox(new Point3D(xPos, yPos + priJoistW / 2 + 5, zPriJoistBottom + uHeadHeightVis / 2), 100, 10, uHeadHeightVis); // U-head right side

                    // Add Bracing (Connecting shores horizontally at mid-height)
                    if (i > 0 && validShores[i - 1, j])
                    {
                        bracingBuilder.AddCylinder(new Point3D(shoreXPositions[i-1], yPos, zShoreBottom + shoreHeight / 2), new Point3D(xPos, yPos, zShoreBottom + shoreHeight / 2), shoreDia * 0.8, 8);
                    }
                }
            }

            // Bracing Y (Connect adjacent shores in Y direction)
            for (int i = 0; i < shoreXPositions.Count; i++)
            {
                for (int j = 1; j < priJoistPositions.Count; j++)
                {
                    if (validShores[i, j - 1] && validShores[i, j])
                    {
                        bracingBuilder.AddCylinder(new Point3D(shoreXPositions[i], priJoistPositions[j-1], zShoreBottom + shoreHeight / 2), new Point3D(shoreXPositions[i], priJoistPositions[j], zShoreBottom + shoreHeight / 2), shoreDia * 0.8, 8);
                    }
                }
            }
            
            group.Children.Add(new GeometryModel3D(shoreBuilder.ToMesh(), shoreMaterial));
            group.Children.Add(new GeometryModel3D(bracingBuilder.ToMesh(), bracingMaterial));
            group.Children.Add(new GeometryModel3D(jackBuilder.ToMesh(), jackMaterial));

            return group;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private bool IsPointInside(System.Windows.Point testPoint, List<List<System.Windows.Point>> polygons)
        {
            if (polygons == null || polygons.Count == 0) return true; // Fallback

            bool inside = false;
            foreach (var poly in polygons)
            {
                for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                {
                    if (((poly[i].Y > testPoint.Y) != (poly[j].Y > testPoint.Y)) &&
                        (testPoint.X < (poly[j].X - poly[i].X) * (testPoint.Y - poly[i].Y) / (poly[j].Y - poly[i].Y) + poly[i].X))
                    {
                        inside = !inside;
                    }
                }
            }
            return inside;
        }

        private List<double> GetYIntersections(double xPos, List<List<System.Windows.Point>> polygons)
        {
            List<double> yInts = new List<double>();
            if (polygons == null) return yInts;

            foreach (var poly in polygons)
            {
                for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                {
                    System.Windows.Point p1 = poly[i];
                    System.Windows.Point p2 = poly[j];

                    if ((p1.X <= xPos && p2.X > xPos) || (p2.X <= xPos && p1.X > xPos))
                    {
                        double t = (xPos - p1.X) / (p2.X - p1.X);
                        double yInt = p1.Y + t * (p2.Y - p1.Y);
                        yInts.Add(yInt);
                    }
                }
            }
            yInts.Sort();
            return yInts;
        }

        private List<double> GetXIntersections(double yPos, List<List<System.Windows.Point>> polygons)
        {
            List<double> xInts = new List<double>();
            if (polygons == null) return xInts;

            foreach (var poly in polygons)
            {
                for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                {
                    System.Windows.Point p1 = poly[i];
                    System.Windows.Point p2 = poly[j];

                    if ((p1.Y <= yPos && p2.Y > yPos) || (p2.Y <= yPos && p1.Y > yPos))
                    {
                        double t = (yPos - p1.Y) / (p2.Y - p1.Y);
                        double xInt = p1.X + t * (p2.X - p1.X);
                        xInts.Add(xInt);
                    }
                }
            }
            xInts.Sort();
            return xInts;
        }

        private bool IsPointInsideOrNear(System.Windows.Point testPoint, List<List<System.Windows.Point>> polygons, double tolerance)
        {
            if (IsPointInside(testPoint, polygons)) return true;

            foreach (var poly in polygons)
            {
                for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
                {
                    System.Windows.Point p1 = poly[i];
                    System.Windows.Point p2 = poly[j];
                    if (DistancePointToSegment(testPoint, p1, p2) <= tolerance)
                        return true;
                }
            }
            return false;
        }

        private double DistancePointToSegment(System.Windows.Point pt, System.Windows.Point p1, System.Windows.Point p2)
        {
            double l2 = Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2);
            if (l2 == 0) return Math.Sqrt(Math.Pow(pt.X - p1.X, 2) + Math.Pow(pt.Y - p1.Y, 2));
            double t = Math.Max(0, Math.Min(1, ((pt.X - p1.X) * (p2.X - p1.X) + (pt.Y - p1.Y) * (p2.Y - p1.Y)) / l2));
            System.Windows.Point proj = new System.Windows.Point(p1.X + t * (p2.X - p1.X), p1.Y + t * (p2.Y - p1.Y));
            return Math.Sqrt(Math.Pow(pt.X - proj.X, 2) + Math.Pow(pt.Y - proj.Y, 2));
        }

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public void Dispose()
        {
            if (_sharedViewModel != null)
            {
                _sharedViewModel.PropertyChanged -= SharedViewModel_PropertyChanged;
                _sharedViewModel.SlabSelectionCompleted -= OnSlabSelectionCompleted;
            }
        }
    }

    public class SimpleMeshBuilder
    {
        public MeshGeometry3D Mesh { get; } = new MeshGeometry3D()
        {
            Positions = new Point3DCollection(),
            TriangleIndices = new Int32Collection()
        };

        public void AddBox(Point3D center, double xLength, double yLength, double zLength)
        {
            int startIndex = Mesh.Positions.Count;

            double w = xLength / 2;
            double l = yLength / 2;
            double h = zLength / 2;

            Point3D[] p = new Point3D[]
            {
                new Point3D(center.X - w, center.Y - l, center.Z + h),
                new Point3D(center.X + w, center.Y - l, center.Z + h),
                new Point3D(center.X + w, center.Y + l, center.Z + h),
                new Point3D(center.X - w, center.Y + l, center.Z + h),
                new Point3D(center.X - w, center.Y - l, center.Z - h),
                new Point3D(center.X + w, center.Y - l, center.Z - h),
                new Point3D(center.X + w, center.Y + l, center.Z - h),
                new Point3D(center.X - w, center.Y + l, center.Z - h)
            };

            foreach (var pt in p) Mesh.Positions.Add(pt);

            int[] indices = new int[]
            {
                0,1,2, 0,2,3, // top
                7,6,5, 7,5,4, // bottom
                4,5,1, 4,1,0, // front
                5,6,2, 5,2,1, // right
                6,7,3, 6,3,2, // back
                7,4,0, 7,0,3  // left
            };

            foreach (var i in indices) Mesh.TriangleIndices.Add(startIndex + i);
        }

        public void AddCylinder(Point3D p1, Point3D p2, double diameter, int thetaDiv)
        {
            double r = diameter / 2;
            Vector3D dir = p2 - p1;
            dir.Normalize();

            Vector3D u = new Vector3D(0, 1, 0);
            if (Math.Abs(Vector3D.DotProduct(u, dir)) > 0.9) u = new Vector3D(1, 0, 0);
            Vector3D v = Vector3D.CrossProduct(dir, u);
            v.Normalize();
            u = Vector3D.CrossProduct(v, dir);
            u.Normalize();

            int startIndex = Mesh.Positions.Count;

            for (int i = 0; i < thetaDiv; i++)
            {
                double theta = 2.0 * Math.PI * i / thetaDiv;
                Vector3D offset = r * Math.Cos(theta) * u + r * Math.Sin(theta) * v;
                Mesh.Positions.Add(p1 + offset);
                Mesh.Positions.Add(p2 + offset);
            }

            for (int i = 0; i < thetaDiv; i++)
            {
                int next = (i + 1) % thetaDiv;
                int p1i = startIndex + i * 2;
                int p2i = startIndex + i * 2 + 1;
                int np1i = startIndex + next * 2;
                int np2i = startIndex + next * 2 + 1;

                Mesh.TriangleIndices.Add(p1i);
                Mesh.TriangleIndices.Add(np1i);
                Mesh.TriangleIndices.Add(p2i);

                Mesh.TriangleIndices.Add(p2i);
                Mesh.TriangleIndices.Add(np1i);
                Mesh.TriangleIndices.Add(np2i);
            }
        }

        public void AddWall(Point3D p1, Point3D p2, double thickness, double height, double zBottom)
        {
            Vector3D dir = p2 - p1;
            dir.Normalize();
            Vector3D n = new Vector3D(-dir.Y, dir.X, 0); // Left normal
            n.Normalize();

            Point3D[] pts = new Point3D[8];
            pts[0] = p1 - n * (thickness / 2) + new Vector3D(0, 0, zBottom + height);
            pts[1] = p2 - n * (thickness / 2) + new Vector3D(0, 0, zBottom + height);
            pts[2] = p2 + n * (thickness / 2) + new Vector3D(0, 0, zBottom + height);
            pts[3] = p1 + n * (thickness / 2) + new Vector3D(0, 0, zBottom + height);

            pts[4] = p1 - n * (thickness / 2) + new Vector3D(0, 0, zBottom);
            pts[5] = p2 - n * (thickness / 2) + new Vector3D(0, 0, zBottom);
            pts[6] = p2 + n * (thickness / 2) + new Vector3D(0, 0, zBottom);
            pts[7] = p1 + n * (thickness / 2) + new Vector3D(0, 0, zBottom);

            int startIndex = Mesh.Positions.Count;
            foreach (var pt in pts) Mesh.Positions.Add(pt);

            int[] indices = new int[]
            {
                0,1,2, 0,2,3, // top
                7,6,5, 7,5,4, // bottom
                4,5,1, 4,1,0, // front
                5,6,2, 5,2,1, // right
                6,7,3, 6,3,2, // back
                7,4,0, 7,0,3  // left
            };

            foreach (var i in indices) Mesh.TriangleIndices.Add(startIndex + i);
        }

        public MeshGeometry3D ToMesh() => Mesh;
    }
}
