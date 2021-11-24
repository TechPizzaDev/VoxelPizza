using System;
using System.Numerics;
using System.Runtime.CompilerServices;
using Veldrid;
using VoxelPizza.Numerics;
using VoxelPizza.World;

namespace VoxelPizza.Client
{
    public static class ShapeMeshHelper
    {
        public const int BoxMaxVertexCount = 64;
        public const int BoxIndexCount = 216;

        public static int GetBoxMesh(
            WorldBox box, float lineWidth,
            RgbaByte color0, RgbaByte color1,
            Span<uint> indexDestination,
            Span<VertexPosition<RgbaByte>> vertexDestination)
        {
            return GetBoxMesh(box.Origin, box.Size, lineWidth, color0, color1, indexDestination, vertexDestination);
        }

        public static int GetBoxMesh(
            Vector3 position, Size3f size, float lineWidth,
            RgbaByte color0, RgbaByte color1,
            Span<uint> indexDestination,
            Span<VertexPosition<RgbaByte>> vertexDestination)
        {
            const uint vertsPerColor = 32;

            int indexOffset = 0;
            uint vertexCount = color0 == color1 ? 0 : vertsPerColor;

            void DrawPrism(
                uint s0,
                uint s1,
                uint s2,
                uint e0,
                uint e1,
                uint e2,
                Span<uint> indices)
            {
                Span<uint> i = indices.Slice(indexOffset, 6 * 3);

                //v[0 + 0].Position = s0,Data = color0;
                //v[1 + 0].Position = e0,Data = color1;
                //v[2 + 0].Position = e1,Data = color0;
                //v[3 + 0].Position = s1,Data = color1;
                i[0 + 0] = s0;
                i[1 + 0] = e0 + vertexCount;
                i[2 + 0] = e1;
                i[3 + 0] = s0;
                i[4 + 0] = e1;
                i[5 + 0] = s1 + vertexCount;

                //v[0 + 4].Position = s1,Data = color0;
                //v[1 + 4].Position = e1,Data = color1;
                //v[2 + 4].Position = e2,Data = color0;
                //v[3 + 4].Position = s2,Data = color1;
                i[0 + 6] = s1;
                i[1 + 6] = e1 + vertexCount;
                i[2 + 6] = e2;
                i[3 + 6] = s1;
                i[4 + 6] = e2;
                i[5 + 6] = s2 + vertexCount;

                //v[0 + 8].Position = s2,Data = color0;
                //v[1 + 8].Position = e2,Data = color1;
                //v[2 + 8].Position = e0,Data = color0;
                //v[3 + 8].Position = s0,Data = color1;
                i[0 + 12] = s2;
                i[1 + 12] = e2 + vertexCount;
                i[2 + 12] = e0;
                i[3 + 12] = s2;
                i[4 + 12] = e0;
                i[5 + 12] = s0 + vertexCount;

                indexOffset += 6 * 3;
            }

            Span<VertexPosition<RgbaByte>> verts = vertexDestination.Slice(0, (int)vertsPerColor);

            verts[0].Position = position + new Vector3(0, 0, 0);                             // s0_v0 
            verts[1].Position = position + new Vector3(lineWidth, 0, 0);                     // s1_v1 
            verts[2].Position = position + new Vector3(0, 0, lineWidth);                     // s2_v2 
            verts[3].Position = position + new Vector3(size.W, 0, 0);                        // s0_v3 
            verts[4].Position = position + new Vector3(size.W, 0, lineWidth);                // s1_v4 
            verts[5].Position = position + new Vector3(size.W - lineWidth, 0, 0);            // s2_v5 
            verts[6].Position = position + new Vector3(size.W, 0, size.D);                   // s0_v6 
            verts[7].Position = position + new Vector3(size.W - lineWidth, 0, size.D);       // s1_v7 
            verts[8].Position = position + new Vector3(size.W, 0, size.D - lineWidth);       // s2_v8 
            verts[9].Position = position + new Vector3(0, 0, size.D);                        // s0_v9 
            verts[10].Position = position + new Vector3(0, 0, size.D - lineWidth);           // s1_v10
            verts[11].Position = position + new Vector3(lineWidth, 0, size.D);               // s2_v11
            verts[12].Position = position + new Vector3(0, lineWidth, 0);                    // e1_v12
            verts[13].Position = position + new Vector3(0, size.H - lineWidth, 0);           // s0_v13
            verts[14].Position = position + new Vector3(0, size.H, lineWidth);               // s1_v14
            verts[15].Position = position + new Vector3(0, size.H, 0);                       // s2_v15
            verts[16].Position = position + new Vector3(0, lineWidth, size.D);               // s1_v16
            verts[17].Position = position + new Vector3(0, size.H - lineWidth, size.D);      // e0_v17
            verts[18].Position = position + new Vector3(0, size.H, size.D - lineWidth);      // e1_v18
            verts[19].Position = position + new Vector3(0, size.H, size.D);                  // e2_v19
            verts[20].Position = position + new Vector3(size.W, lineWidth, 0);               // e1_v20
            verts[21].Position = position + new Vector3(size.W, size.H, 0);                  // s0_v21
            verts[22].Position = position + new Vector3(size.W, size.H - lineWidth, 0);      // s1_v22
            verts[23].Position = position + new Vector3(size.W - lineWidth, size.H, 0);      // s2_v23
            verts[24].Position = position + new Vector3(lineWidth, size.H, 0);               // s0_v24
            verts[25].Position = position + new Vector3(size.W, size.H, lineWidth);          // e1_v25
            verts[26].Position = position + new Vector3(size.W, size.H, size.D - lineWidth); // e2_v26
            verts[27].Position = position + new Vector3(size.W, size.H, size.D);             // e0_v27
            verts[28].Position = position + new Vector3(size.W, lineWidth, size.D);          // e1_v28
            verts[29].Position = position + new Vector3(size.W, size.H - lineWidth, size.D); // e1_v29
            verts[30].Position = position + new Vector3(size.W - lineWidth, size.H, size.D); // e2_v30
            verts[31].Position = position + new Vector3(lineWidth, size.H, size.D);          // e0_v31

            if (vertexCount != 0)
            {
                for (int i = 0; i < vertsPerColor; i++)
                {
                    verts[i].Data = color0;
                }

                Span<VertexPosition<RgbaByte>> extraVerts = vertexDestination.Slice((int)vertsPerColor, (int)vertsPerColor);
                for (int i = 0; i < vertsPerColor; i++)
                {
                    extraVerts[i].Position = verts[i].Position;
                    extraVerts[i].Data = color1;
                }
            }
            else
            {
                for (int i = 0; i < vertsPerColor; i++)
                {
                    verts[i].Data = color0;
                }
            }

            #region Vertical Y prisms

            DrawPrism(
                0, 1, 2,
                15, 24, 14,
                indexDestination);

            DrawPrism(
                3, 4, 5,
                21, 25, 23,
                indexDestination);

            DrawPrism(
                6, 7, 8,
                27, 30, 26,
                indexDestination);

            DrawPrism(
                9, 10, 11,
                19, 18, 31,
                indexDestination);

            #endregion

            #region Horizontal X prisms

            DrawPrism(
                3, 20, 4,
                0, 12, 2,
                indexDestination);

            DrawPrism(
                13, 14, 15,
                22, 25, 21,
                indexDestination);

            DrawPrism(
                9, 16, 10,
                6, 28, 8,
                indexDestination);

            DrawPrism(
                29, 26, 27,
                17, 18, 19,
                indexDestination);

            #endregion

            #region Horizontal Z prisms

            DrawPrism(
                9, 11, 16,
                0, 1, 12,
                indexDestination);

            DrawPrism(
                6, 28, 7,
                3, 20, 5,
                indexDestination);

            DrawPrism(
                21, 22, 23,
                27, 29, 30,
                indexDestination);

            DrawPrism(
                24, 13, 15,
                31, 17, 19,
                indexDestination);

            #endregion

            vertexCount += vertsPerColor;

            return (int)vertexCount;
        }

        public static unsafe void DrawPrism(
            GeometryBatch<VertexPosition<RgbaByte>> batch,
            Vector3 s0,
            Vector3 s1,
            Vector3 s2,
            Vector3 e0,
            Vector3 e1,
            Vector3 e2,
            RgbaByte color0,
            RgbaByte color1,
            RgbaByte color2,
            RgbaByte color3)
        {
            GeometryBatch<VertexPosition<RgbaByte>>.UnsafeReserve reserve = batch.ReserveQuadsUnsafe(3);

            VertexPosition<RgbaByte>* ptr0 = reserve.Vertices;
            VertexPosition<RgbaByte>* ptr1 = reserve.Vertices + 4;
            VertexPosition<RgbaByte>* ptr2 = reserve.Vertices + 8;

            SetQuad(ptr0,
                s0, e0, e1, s1,
                color0, color1, color2, color3);

            SetQuad(ptr1,
                s1, e1, e2, s2,
                color0, color1, color2, color3);

            SetQuad(ptr2,
                s2, e2, e0, s0,
                color0, color1, color2, color3);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetRotatedQuad<T>(
            VertexPosition<T>* ptr,
            float x,
            float y,
            float z,
            float dx,
            float dy,
            float w,
            float h,
            float sin,
            float cos,
            T brData,
            T blData,
            T tlData,
            T trData)
            where T : unmanaged, IEquatable<T>
        {
            SetQuad(
                ptr,
                brX: x + (dx + w) * cos - (dy + h) * sin,
                brY: y + (dx + w) * sin + (dy + h) * cos,
                brZ: z,
                blX: x + dx * cos - (dy + h) * sin,
                blY: y + dx * sin + (dy + h) * cos,
                blZ: z,
                tlX: x + dx * cos - dy * sin,
                tlY: y + dx * sin + dy * cos,
                tlZ: z,
                trX: x + (dx + w) * cos - dy * sin,
                trY: y + (dx + w) * sin + dy * cos,
                trZ: z,
                brData,
                blData,
                tlData,
                trData);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetQuad<T>(
            VertexPosition<T>* ptr,
            float brX,
            float brY,
            float brZ,
            float blX,
            float blY,
            float blZ,
            float tlX,
            float tlY,
            float tlZ,
            float trX,
            float trY,
            float trZ,
            T brData,
            T blData,
            T tlData,
            T trData)
            where T : unmanaged, IEquatable<T>
        {
            // bottom-right
            ptr[0].Position.X = brX;
            ptr[0].Position.Y = brY;
            ptr[0].Position.Z = brZ;
            ptr[0].Data = brData;

            // bottom-left
            ptr[1].Position.X = blX;
            ptr[1].Position.Y = blY;
            ptr[1].Position.Z = blZ;
            ptr[1].Data = blData;

            // top-left
            ptr[2].Position.X = tlX;
            ptr[2].Position.Y = tlY;
            ptr[2].Position.Z = tlZ;
            ptr[2].Data = tlData;

            // top-right
            ptr[3].Position.X = trX;
            ptr[3].Position.Y = trY;
            ptr[3].Position.Z = trZ;
            ptr[3].Data = trData;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe void SetQuad<T>(
            VertexPosition<T>* ptr,
            Vector3 br,
            Vector3 bl,
            Vector3 tl,
            Vector3 tr,
            T brData,
            T blData,
            T tlData,
            T trData)
            where T : unmanaged, IEquatable<T>
        {
            // bottom-right
            ptr[0].Position = br;
            ptr[0].Data = brData;

            // bottom-left
            ptr[1].Position = bl;
            ptr[1].Data = blData;

            // top-left
            ptr[2].Position = tl;
            ptr[2].Data = tlData;

            // top-right
            ptr[3].Position = tr;
            ptr[3].Data = trData;
        }
    }
}
