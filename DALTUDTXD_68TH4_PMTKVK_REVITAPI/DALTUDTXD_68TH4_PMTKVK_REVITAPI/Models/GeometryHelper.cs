using System.Collections.Generic;
using System.Windows;

namespace DALTUDTXD_68TH4_PMTKVK_REVITAPI.Models
{
    public static class GeometryHelper
    {
        /// <summary>
        /// Xác Ä‘á»‹nh xem má»™t Ä‘iá»ƒm (X, Y) có nằm trong danh sách các Ä‘a giác hay không bằng thuật toán Ray-Casting.
        /// </summary>
        public static bool IsPointInPolygons(Point point, List<List<Point>> polygons)
        {
            if (polygons == null || polygons.Count == 0) 
                return true; // Fallback: Nếu không Ä‘ọc Ä‘ược biên dạng thì vẽ toàn bá»™ Ä‘á»ƒ tránh lá»—i mất hình

            foreach (var polygon in polygons)
            {
                if (IsPointInPolygon(point, polygon))
                {
                    return true; // Äiá»ƒm nằm trong ít nhất 1 mảnh sàn
                }
            }
            return false;
        }

        private static bool IsPointInPolygon(Point point, List<Point> polygon)
        {
            bool isInside = false;
            int n = polygon.Count;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                if (((polygon[i].Y > point.Y) != (polygon[j].Y > point.Y)) &&
                    (point.X < (polygon[j].X - polygon[i].X) * (point.Y - polygon[i].Y) / (polygon[j].Y - polygon[i].Y) + polygon[i].X))
                {
                    isInside = !isInside;
                }
            }
            return isInside;
        }
    }
}


