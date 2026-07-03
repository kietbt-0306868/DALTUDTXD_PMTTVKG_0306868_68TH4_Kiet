using System;
using System.Collections.Generic;
using Autodesk.Revit.DB;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models
{
    public static class FormworkGeometryUtils
    {
        /// <summary>
        /// Lấy danh sách các CurveLoop thông thủy của mặt đáy sàn sau khi trừ đi phần dầm chiếm chỗ.
        /// Sử dụng để làm lưới (Grid) rải các Family tấm ván khuôn.
        /// </summary>
        /// <param name="floor">Đối tượng Floor (Sàn).</param>
        /// <param name="beams">Danh sách các đối tượng dầm (hoặc đối tượng cản trở) cắt qua hoặc đỡ sàn này.</param>
        /// <returns>Danh sách CurveLoop đại diện cho diện tích ván khuôn đáy sàn.</returns>
        public static List<CurveLoop> GetClearFloorBottomCurveLoops(Floor floor, List<Element> beams)
        {
            List<CurveLoop> clearCurveLoops = new List<CurveLoop>();

            // 1. Lấy các tham chiếu mặt đáy của sàn
            IList<Reference> bottomFaceRefs = HostObjectUtils.GetBottomFaces(floor);
            if (bottomFaceRefs == null || bottomFaceRefs.Count == 0)
                return clearCurveLoops;

            // Thiết lập tùy chọn để lấy Geometry có độ chi tiết cao
            Options geomOptions = new Options() 
            { 
                ComputeReferences = true, 
                DetailLevel = ViewDetailLevel.Fine 
            };

            GeometryElement floorGeom = floor.get_Geometry(geomOptions);
            if (floorGeom == null) return clearCurveLoops;

            // Duyệt qua từng mặt đáy của sàn (Sàn có thể có nhiều mặt đáy nếu bị đục lỗ hoặc chia cắt)
            foreach (Reference refFace in bottomFaceRefs)
            {
                GeometryObject geoObj = floor.GetGeometryObjectFromReference(refFace);
                PlanarFace bottomFace = geoObj as PlanarFace;
                
                if (bottomFace == null) continue;

                // 2. Trích xuất biên dạng (CurveLoop) của mặt đáy sàn
                IList<CurveLoop> floorLoops = bottomFace.GetEdgesAsCurveLoops();
                
                // 3. Tạo Solid "Không gian ảo" từ biên dạng đáy sàn
                // Đẩy (Extrude) biên dạng đáy sàn hướng xuống dưới để tạo không gian dưới sàn.
                XYZ extrusionDir = bottomFace.FaceNormal; // Pháp tuyến đáy sàn hướng xuống (thường là Z = -1)
                
                // Tính extrusionDist động dựa trên chiều cao dầm lớn nhất (thay vì hardcode 2.0 ft)
                // Điều này đảm bảo dầm sâu (>600mm, ví dụ dầm chính nhịp ≥8m) được trừ hết.
                double maxBeamDepthFt = 0;
                foreach (Element beam in beams)
                {
                    BoundingBoxXYZ bb = beam.get_BoundingBox(null);
                    if (bb != null)
                    {
                        double beamHeight = bb.Max.Z - bb.Min.Z; // feet
                        if (beamHeight > maxBeamDepthFt)
                            maxBeamDepthFt = beamHeight;
                    }
                }
                // Hệ số an toàn 1.5 để đảm bảo bao trùm hoàn toàn, tối thiểu 2.0 ft (~600mm)
                double extrusionDist = Math.Max(2.0, maxBeamDepthFt * 1.5);
                
                Solid floorClearSpaceSolid = null;
                try
                {
                    floorClearSpaceSolid = GeometryCreationUtilities.CreateExtrusionGeometry(floorLoops, extrusionDir, extrusionDist);
                }
                catch (Exception)
                {
                    // Bỏ qua nếu CurveLoop lỗi (ví dụ: mép tự giao cắt)
                    continue;
                }

                if (floorClearSpaceSolid == null || floorClearSpaceSolid.Volume <= 0) 
                    continue;

                Solid remainingSolid = floorClearSpaceSolid;

                // 4. Lần lượt trừ (Difference) Solid của các dầm khỏi "Không gian ảo"
                foreach (Element beam in beams)
                {
                    List<Solid> beamSolids = GetElementSolids(beam, geomOptions);
                    foreach (Solid bSolid in beamSolids)
                    {
                        try
                        {
                            // Lấy khối còn lại trừ đi phần dầm chiếm chỗ
                            remainingSolid = BooleanOperationsUtils.ExecuteBooleanOperation(
                                remainingSolid, bSolid, BooleanOperationsType.Difference);
                        }
                        catch (Autodesk.Revit.Exceptions.InvalidOperationException)
                        {
                            // Bỏ qua nếu không thể boolean (thường do dầm không chạm sàn hoặc lỗi hình học)
                            continue;
                        }
                    }
                }

                // 5. Trích xuất CurveLoop từ các mặt (Face) phù hợp của khối còn lại
                if (remainingSolid != null && remainingSolid.Volume > 0)
                {
                    foreach (Face face in remainingSolid.Faces)
                    {
                        PlanarFace pFace = face as PlanarFace;
                        if (pFace != null)
                        {
                            // Đặc điểm của mặt ta cần tìm: 
                            // 1. Phải song song và có pháp tuyến ngược với hướng đẩy (nghĩa là hướng ngược lại của mặt đáy cũ)
                            // Nên pFace.FaceNormal phải ngược chiều với bottomFace.FaceNormal ban đầu
                            if (pFace.FaceNormal.IsAlmostEqualTo(-bottomFace.FaceNormal))
                            {
                                // 2. Phải cùng cao độ (cùng mặt phẳng) với đáy sàn ban đầu
                                // Tính khoảng cách hình chiếu giữa 2 điểm Origin theo phương pháp tuyến
                                double projectedDist = Math.Abs((pFace.Origin - bottomFace.Origin).DotProduct(bottomFace.FaceNormal));
                                
                                if (projectedDist < 1e-4) // Dung sai cho phép (sai số hệ thống ~0.03mm)
                                {
                                    // Vùng này chính là diện tích thông thủy cần tìm
                                    IList<CurveLoop> loops = pFace.GetEdgesAsCurveLoops();
                                    clearCurveLoops.AddRange(loops);
                                }
                            }
                        }
                    }
                }
            }

            return clearCurveLoops;
        }

        /// <summary>
        /// Hàm phụ trợ: Lấy danh sách các khối Solid tạo nên một Element
        /// Một đối tượng có thể bao gồm nhiều Solid rời rạc (ví dụ dầm bị đục lỗ, hoặc chia khúc).
        /// </summary>
        private static List<Solid> GetElementSolids(Element element, Options options)
        {
            List<Solid> solids = new List<Solid>();
            GeometryElement geomElem = element.get_Geometry(options);
            
            if (geomElem == null) return solids;

            foreach (GeometryObject geomObj in geomElem)
            {
                if (geomObj is Solid solid && solid.Volume > 0)
                {
                    solids.Add(solid);
                }
                else if (geomObj is GeometryInstance geomInst)
                {
                    // Lấy hình học thực tế (đã biến đổi tọa độ) trong project thay vì trong family
                    GeometryElement instGeom = geomInst.GetInstanceGeometry();
                    foreach (GeometryObject instObj in instGeom)
                    {
                        if (instObj is Solid instSolid && instSolid.Volume > 0)
                        {
                            solids.Add(instSolid);
                        }
                    }
                }
            }
            return solids;
        }
    }
}
