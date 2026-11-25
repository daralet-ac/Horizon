using System.Collections.Generic;
using Microsoft.DirectX.Direct3D;

namespace Horizon.Ui {
    /// <summary>
    /// A scenery model's own geometry for the far trees (FarTreeModels): its vertices (position and texture place, 20
    /// bytes) and triangles, both in managed buffers, in parts by texture.
    /// </summary>
    internal sealed class FarTreeModel {
        public VertexBuffer Vertices;
        public IndexBuffer Indices;
        public int VertexCount;
        public readonly List<FarTreePart> Parts = new List<FarTreePart>();
    }
}
