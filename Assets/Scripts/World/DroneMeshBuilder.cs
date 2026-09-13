using UnityEngine;

namespace RemoteChase.World
{
    /// <summary>
    /// Gera por codigo a malha do drone: um prisma triangular achatado (um
    /// "triangulo" com espessura) apontando para +Z. Evita depender de asset externo.
    /// </summary>
    public static class DroneMeshBuilder
    {
        /// <summary>
        /// Cria a malha do drone.
        /// </summary>
        /// <param name="length">Comprimento total, do nariz a traseira.</param>
        /// <param name="width">Largura da traseira.</param>
        /// <param name="thickness">Espessura do prisma.</param>
        public static Mesh CreateArrow(float length = 2f, float width = 1.6f, float thickness = 0.35f)
        {
            float noseZ = length * 0.6f;
            float tailZ = -length * 0.4f;
            float halfWidth = width * 0.5f;
            float halfThickness = thickness * 0.5f;

            // Triangulo visto de cima: nariz a frente, dois cantos atras.
            Vector3 noseTop = new Vector3(0f, halfThickness, noseZ);
            Vector3 rightTop = new Vector3(halfWidth, halfThickness, tailZ);
            Vector3 leftTop = new Vector3(-halfWidth, halfThickness, tailZ);

            Vector3 noseBottom = new Vector3(0f, -halfThickness, noseZ);
            Vector3 rightBottom = new Vector3(halfWidth, -halfThickness, tailZ);
            Vector3 leftBottom = new Vector3(-halfWidth, -halfThickness, tailZ);

            var vertices = new System.Collections.Generic.List<Vector3>(18);
            var triangles = new System.Collections.Generic.List<int>(24);
            var uvs = new System.Collections.Generic.List<Vector2>(18);

            // Topo e base. Cada face tem seus proprios vertices para dar
            // sombreamento chapado (flat), que combina com o visual blocado.
            AddTriangle(vertices, triangles, uvs, noseTop, rightTop, leftTop);
            AddTriangle(vertices, triangles, uvs, noseBottom, leftBottom, rightBottom);

            // Laterais: um quad por aresta do triangulo.
            AddQuad(vertices, triangles, uvs, noseTop, noseBottom, rightBottom, rightTop);
            AddQuad(vertices, triangles, uvs, rightTop, rightBottom, leftBottom, leftTop);
            AddQuad(vertices, triangles, uvs, leftTop, leftBottom, noseBottom, noseTop);

            var mesh = new Mesh { name = "DroneArrow" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, uvs);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        static void AddTriangle(System.Collections.Generic.List<Vector3> vertices,
                                System.Collections.Generic.List<int> triangles,
                                System.Collections.Generic.List<Vector2> uvs,
                                Vector3 a, Vector3 b, Vector3 c)
        {
            int start = vertices.Count;

            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);

            uvs.Add(new Vector2(0.5f, 1f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(0f, 0f));

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        static void AddQuad(System.Collections.Generic.List<Vector3> vertices,
                            System.Collections.Generic.List<int> triangles,
                            System.Collections.Generic.List<Vector2> uvs,
                            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            int start = vertices.Count;

            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);

            uvs.Add(new Vector2(0f, 1f));
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));

            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);

            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }
    }
}
