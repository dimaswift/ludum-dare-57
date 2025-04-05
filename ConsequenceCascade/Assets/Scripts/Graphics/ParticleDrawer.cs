using UnityEngine;

namespace ConsequenceCascade.Graphics
{

    public class ParticleDrawer
    {
        private readonly ComputeBuffer meshBuffer;
        private readonly Material material;
        private readonly Mesh mesh;

        public ParticleDrawer(Material mat, Mesh m, ComputeBuffer buffer)
        {
            mesh = m;
            var args = new uint[] { 0, 0, 0, 0, 0 };
            args[0] = mesh.GetIndexCount(0);
            args[1] = (uint)buffer.count;
            args[2] = mesh.GetIndexStart(0);
            args[3] = mesh.GetBaseVertex(0);
            meshBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);
            meshBuffer.SetData(args);
            material = mat;
            material.SetBuffer("Particles", buffer);
        }

        public void Draw()
        {
            UnityEngine.Graphics.DrawMeshInstancedIndirect(mesh, 0, material,
                new Bounds(Vector3.zero, Vector3.one * 1000), meshBuffer);
        }
    }
}