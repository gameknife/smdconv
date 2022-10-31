﻿// a program to trans id-deamon's ascii custom model file to smd file format
// with external instance & material infos

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;


namespace smdconv
{
    public class MaterialInfo
    {
        public string hash { get; set; }
        public string[] BaseColorTextures { get; set; }
        public string[] BaseNormalTextures { get; set; }
        public string[] BasePackTextures { get; set; }
        public string[] BaseTranspTextures { get; set; }
    }
    struct MeshStatic
    {
        public int triCount;
        public int meshCount;
    }

    struct Transform
    {
        public Transform(Vector3 l, Quaternion r, Vector3 s)
        {
            Location = l;
            Rotation = r;
            Scale = s;
        }
        public Vector3 Location;
        public Quaternion Rotation;
        public Vector3 Scale;
    }
    class Program
    {
        private static String m_Title = "custom ascii mesh to smd tools";
        private static bool SkipInstance = true;
        private static bool ExtractNode = true;

        public static byte[] GetHash(string inputString)
        {
            using (HashAlgorithm algorithm = SHA256.Create())
                return algorithm.ComputeHash(Encoding.UTF8.GetBytes(inputString));
        }

        public static string GetHashString(string inputString)
        {
            StringBuilder sb = new StringBuilder();
            foreach (byte b in GetHash(inputString))
                sb.Append(b.ToString("X2"));

            return sb.ToString();
        }

        static void Main(string[] args)
        {
            MeshStatic statistic = new MeshStatic();
            statistic.triCount = 0;
            statistic.meshCount = 0;

            Console.Title = m_Title;
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine(m_Title);
            Console.ResetColor();

            if (args.Length < 2)
            {
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine("[Usage]");
                Console.WriteLine("    U4.Unpacker <m_File> <m_Directory>\n");
                Console.WriteLine("    m_File - Source of PSARC file");
                Console.WriteLine("    m_Directory - Destination directory\n");
                Console.ResetColor();
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("[Examples]");
                Console.WriteLine("    U4.Unpacker E:\\Games\\U4\\Uncharted4_data\\data\\fonts.psarc D:\\Unpacked");
                Console.ResetColor();
                return;
            }

            // 输入命令行处理
            String[] SrcMeshFiles = args;
            String SrcMeshFileRoot = args[1];
            String SrcShaderFileRoot = args[0];
            
            if (!File.Exists(SrcMeshFileRoot))
            {
                Console.WriteLine("[Error] Src File Not Exist!");
                return;
            }
            
            // 材质parse
            String DstMaterialFile = Path.GetFileNameWithoutExtension(SrcMeshFileRoot) + ".smd.mat";
            DstMaterialFile = Path.Join(Path.GetDirectoryName(SrcMeshFileRoot), DstMaterialFile);
            Console.WriteLine("Converting {0} -> {1}", SrcMeshFileRoot, DstMaterialFile);
            
            List<string> KeyUniqueList = new List<string>();
            
            var ShaderFileStream = File.OpenRead(SrcShaderFileRoot);
            var ShaderFileReader = new StreamReader(ShaderFileStream, Encoding.UTF8, true);

            Dictionary<string, MaterialInfo> materials = new Dictionary<string, MaterialInfo>();

            {
                String line;
                
                line = ShaderFileReader.ReadLine();
                int shaderNumber = Int32.Parse( line.Split(' ')[0]);
                
                // eat the first divider
                line = ShaderFileReader.ReadLine();
                
                for (int i = 0; i < shaderNumber; ++i)
                {
                    // skip the header
                    line = ShaderFileReader.ReadLine();
                    line = ShaderFileReader.ReadLine();

                    // fetch valid texture and make hash
                    var mat = new MaterialInfo();

                    List<string> colormap = new List<string>();
                    List<string> normalmap = new List<string>();
                    List<string> packmap = new List<string>();
                    List<string> transpmap = new List<string>();

                    string textureGroup = "";
                    
                    while (true)
                    {
                        line = ShaderFileReader.ReadLine();
                        if (line == "================================================" || line == null)
                        {
                            break;
                        }

                        var keyvalue = line.Split('\t');
                        if (keyvalue.Length == 2 && keyvalue[1] != "")
                        {
                            if (!keyvalue[0].StartsWith("g_tLm"))
                            {
                                // remap the textuew
                                if (Regex.IsMatch(keyvalue[0], @"g_tNdFetchBaseColor.*Map", RegexOptions.IgnoreCase))
                                {
                                    colormap.Add(keyvalue[1]);
                                }
                                if (Regex.IsMatch(keyvalue[0], @"g_tNdFetchNormal.*Map", RegexOptions.IgnoreCase))
                                {
                                    normalmap.Add(keyvalue[1]);
                                }
                                if (Regex.IsMatch(keyvalue[0], @"g_tNdFetchPack.*Map", RegexOptions.IgnoreCase))
                                {
                                    packmap.Add(keyvalue[1]);
                                }
                                if (Regex.IsMatch(keyvalue[0], @"g_tNdFetchTransparency.*Map", RegexOptions.IgnoreCase))
                                {
                                    transpmap.Add(keyvalue[1]);
                                }
                                if (!KeyUniqueList.Contains(keyvalue[0]))
                                {
                                    KeyUniqueList.Add(keyvalue[0]);
                                }

                                textureGroup += keyvalue[1];
                            }
                        }
                    }
                    
                    mat.hash = GetHashString(textureGroup);
                    mat.BaseColorTextures = colormap.ToArray();
                    mat.BaseNormalTextures = normalmap.ToArray();
                    mat.BasePackTextures = packmap.ToArray();
                    mat.BaseTranspTextures = transpmap.ToArray();

                    if (!materials.ContainsKey(mat.hash))
                    {
                        materials.Add( mat.hash, mat );    
                    }
                }
            }
            
            var options = new JsonSerializerOptions { WriteIndented = true };
            string jsonString = JsonSerializer.Serialize(materials, options);
            
            File.WriteAllText( DstMaterialFile, jsonString);
            
            int materialCount = 0;
            int meshCount = 0;
            
            // 基础map建立
            Dictionary<string, KeyValuePair<int, float>> existToken = new Dictionary<string, KeyValuePair<int, float>>();
            Dictionary<string, string> existMaterialToken = new Dictionary<string, string>();
            List<Vector3> instanceLocationList = new List<Vector3>();
            List<Quaternion> instanceRotationList = new List<Quaternion>();

            List<KeyValuePair<int, Transform>> realInstanceList =
                new List<KeyValuePair<int, Transform>>();

            // instance transform file ( submesh id -> transform )

            // material texture file


            // basic smd file
            String DstMeshFile = Path.GetFileNameWithoutExtension(SrcMeshFileRoot) + ".smd";
            DstMeshFile = Path.Join(Path.GetDirectoryName(SrcMeshFileRoot), DstMeshFile);
            Console.WriteLine("Converting {0} -> {1}", SrcMeshFileRoot, DstMeshFile);

            File.Delete(DstMeshFile);
            var outFileStream = File.OpenWrite(DstMeshFile);
            var streamWriter = new StreamWriter(outFileStream, Encoding.UTF8);

            // version 1
            // nodes 
            //     end
            streamWriter.WriteLine("version 1");

            streamWriter.WriteLine("nodes");
            streamWriter.WriteLine("0 root -1");
            streamWriter.WriteLine("end");

            const Int32 BufferSize = 1024;

            for (int file = 1; file < SrcMeshFiles.Length; ++file)
            {
                var fileStream = File.OpenRead(SrcMeshFiles[file]);
                var streamReader = new StreamReader(fileStream, Encoding.UTF8, true, BufferSize);
                String line;
                line = streamReader.ReadLine();

                int magic = Int32.Parse(line);
                line = streamReader.ReadLine();
                int SubMeshCount = Int32.Parse(line);

                meshCount += SubMeshCount;

                for (int i = 0; i < SubMeshCount; ++i)
                {
                    line = streamReader.ReadLine();
                    var meshName = line;

                    // texcoord
                    line = streamReader.ReadLine();
                    int TexcoordCount = Int32.Parse(line);

                    // texture
                    line = streamReader.ReadLine();
                    int textureCount = Int32.Parse(line);
                    string textureGroup = "";
                    for (int tx = 0; tx < textureCount; ++tx)
                    {
                        // fast skip
                        line = streamReader.ReadLine();
                        textureGroup += line;
                        line = streamReader.ReadLine();
                    }

                    var materialName = "mat";

                    var hash = GetHashString(textureGroup);

                    if (existMaterialToken.ContainsKey(hash))
                    {
                        materialName = existMaterialToken[hash];
                    }
                    else
                    {
                        //Console.WriteLine(hash);


                        materialName = String.Format("mat_{0}", materialCount++);
                        existMaterialToken.Add(hash, materialName);
                        
                        //Console.WriteLine(hash);
                    }

                    // vertex
                    line = streamReader.ReadLine();
                    int VertexCount = Int32.Parse(line);

                    Vector3[] Vertice = new Vector3[VertexCount];
                    Vector3[] Normals = new Vector3[VertexCount];
                    Vector2[] TexCoord0 = new Vector2[VertexCount];

                    for (int v = 0; v < VertexCount; ++v)
                    {
                        while (true)
                        {
                            line = streamReader.ReadLine();
                            // 这里，有可能会有几行奇怪的数据，例如0 0 0 0 0 0 0 0，如果split出来不是v3，就跳过     
                            if (line.Split(' ').Length == 3)
                            {
                                break;
                            }
                        }
                        var v3 = line.Split(' ');
                        Vertice[v] = new Vector3(float.Parse(v3[0]), -float.Parse(v3[2]), float.Parse(v3[1]));

                        line = streamReader.ReadLine();
                        v3 = line.Split(' ');
                        Normals[v] = new Vector3(float.Parse(v3[0]), -float.Parse(v3[2]), float.Parse(v3[1]));

                        // vertex color
                        line = streamReader.ReadLine();
                        if (TexcoordCount > 0)
                        {
                            for (int tc = 0; tc < TexcoordCount; ++tc)
                            {
                                line = streamReader.ReadLine();
                                if (tc == 0)
                                {
                                    var v2 = line.Split(' ');
                                    TexCoord0[v] = new Vector2(float.Parse(v2[0]), float.Parse(v2[1]));
                                }
                            }
                        }
                        else
                        {
                            TexCoord0[v] = Vector2.Zero;
                        }
                    }

                    // tri
                    while (true)
                    {
                        line = streamReader.ReadLine();
                        // 这里，有可能会有几行奇怪的数据，例如0 0 0 0 0 0 0 0，如果split出来不是v3，就跳过     
                        if (line.Split(' ').Length == 1)
                        {
                            break;
                        }
                    }
                    int TriCount = Int32.Parse(line);

               

                    bool writeSubmesh = true;

                    // 用第一个三角面，来恢复transform，做法：
                    // tri1，以第一个顶点为origin，构造一个齐次坐标系M1
                    // tri2，以第一个顶点为origin，构造一个齐次坐标系M2
                    // M = M2 * invM1

                    // tri1，先将他归还到原点
                    float DotResult = 0;
                    var Side1Length = CalcLocalPose(Vertice, out var worldMat, out DotResult);
                    
                    // 这个token不够用了，应该是有完全相同的独立instance，再加入一个差集角度
                    string token = String.Format("{0}_{1}_{2}", VertexCount, TriCount, Math.Floor(DotResult * 100));
                    
                    Matrix4x4 worldMatInv;
                    Matrix4x4.Invert(worldMat, out worldMatInv);

                    var translation = new Vector3();
                    var rotation = new Quaternion();
                    var scaleT = new Vector3();

                    Matrix4x4.Decompose(worldMat, out scaleT, out rotation, out translation);
                    // Console.WriteLine("Translation: {0}",translation);
                    // Console.WriteLine("Rotation: {0}", rotation.ToEulerAngles());
                    // Console.WriteLine("Scale: {0}", scale);

                    if (SkipInstance)
                    {
                        if (existToken.ContainsKey(token))
                        {
                            writeSubmesh = false;

                            float scale = Side1Length / existToken[token].Value;
                            //Console.WriteLine("scale: {0}",scale);

                            Vector3 scale3 = new Vector3(scale);
                            
                            realInstanceList.Add(new KeyValuePair<int, Transform>(existToken[token].Key, new Transform(translation, rotation, scale3)));
                        }
                        else
                        {
                            var currIdx = existToken.Count;
                            existToken.Add(token, new KeyValuePair<int, float>(currIdx,Side1Length));
                        }
                    }

                    if (writeSubmesh)
                    {
                        streamWriter.WriteLine("triangles");
                        statistic.triCount += TriCount;
                        statistic.meshCount++;
                    }

                    if (writeSubmesh)
                    {
                        for (int v = 0; v < Vertice.Length; ++v)
                        {
                            Vertice[v] = Vector3.Transform(Vertice[v], worldMatInv);
                            Normals[v] = Vector3.TransformNormal(Normals[v], worldMatInv);
                        }

                        instanceLocationList.Add(translation);
                        instanceRotationList.Add(Quaternion.Normalize(rotation));
                    }

                    for (int t = 0; t < TriCount; ++t)
                    {
                        line = streamReader.ReadLine();

                        if (writeSubmesh)
                        {
                            var triIdx = line.Split(' ');

                            streamWriter.WriteLine(materialName);

                            {
                                var v1 = Vertice[Int32.Parse(triIdx[0])];
                                var n1 = Normals[Int32.Parse(triIdx[0])];
                                var t1 = TexCoord0[Int32.Parse(triIdx[0])];
                                streamWriter.WriteLine(String.Format("0  {0} {1} {2}  {3} {4} {5}  {6} {7}", v1.X, v1.Y,
                                    v1.Z, n1.X, n1.Y, n1.Z, t1.X, t1.Y));
                            }

                            {
                                var v1 = Vertice[Int32.Parse(triIdx[2])];
                                var n1 = Normals[Int32.Parse(triIdx[2])];
                                var t1 = TexCoord0[Int32.Parse(triIdx[2])];
                                streamWriter.WriteLine(String.Format("0  {0} {1} {2}  {3} {4} {5}  {6} {7}", v1.X, v1.Y,
                                    v1.Z, n1.X, n1.Y, n1.Z, t1.X, t1.Y));
                            }

                            {
                                var v1 = Vertice[Int32.Parse(triIdx[1])];
                                var n1 = Normals[Int32.Parse(triIdx[1])];
                                var t1 = TexCoord0[Int32.Parse(triIdx[1])];
                                streamWriter.WriteLine(String.Format("0  {0} {1} {2}  {3} {4} {5}  {6} {7}", v1.X, v1.Y,
                                    v1.Z, n1.X, n1.Y, n1.Z, t1.X, t1.Y));
                            }
                        }
                    }

                    // close
                    if (writeSubmesh)
                    {
                        streamWriter.WriteLine("end");
                    }
                }
            }

            streamWriter.Flush();
            streamWriter.Close();
            outFileStream.Close();

            {
                string DstInstanceFile = Path.GetFileNameWithoutExtension(SrcMeshFileRoot) + ".smd.level";
                DstInstanceFile = Path.Join(Path.GetDirectoryName(SrcMeshFileRoot), DstInstanceFile);

                File.Delete(DstInstanceFile);
                outFileStream = File.OpenWrite(DstInstanceFile);
                streamWriter = new StreamWriter(outFileStream, Encoding.UTF8);

                streamWriter.WriteLine("SMD level transforms");
                // write instance transform
                for (int i = 0; i < instanceLocationList.Count; i++)
                {
                    streamWriter.WriteLine("{0} {1} {2} {3} {4} {5} {6}", instanceLocationList[i].X, instanceLocationList[i].Y, instanceLocationList[i].Z, instanceRotationList[i].X, instanceRotationList[i].Y, instanceRotationList[i].Z,
                        instanceRotationList[i].W);
                }

                streamWriter.Flush();
                streamWriter.Close();
                outFileStream.Close();
            }
            
            {
                string DstInstanceFile = Path.GetFileNameWithoutExtension(SrcMeshFileRoot) + ".smd.levelinstance";
                DstInstanceFile = Path.Join(Path.GetDirectoryName(SrcMeshFileRoot), DstInstanceFile);

                File.Delete(DstInstanceFile);
                outFileStream = File.OpenWrite(DstInstanceFile);
                streamWriter = new StreamWriter(outFileStream, Encoding.UTF8);

                // write instance transform
                streamWriter.WriteLine("SMD level instance transforms");

                for (int i = 0; i < realInstanceList.Count; i++)
                {
                    var inst = realInstanceList[i];

                    streamWriter.WriteLine("{0} {1} {2} {3} {4} {5} {6} {7} {8} {9} {10}", inst.Key,
                        realInstanceList[i].Value.Location.X, realInstanceList[i].Value.Location.Y, realInstanceList[i].Value.Location.Z,
                        realInstanceList[i].Value.Rotation.X, realInstanceList[i].Value.Rotation.Y, realInstanceList[i].Value.Rotation.Z, realInstanceList[i].Value.Rotation.W,
                        realInstanceList[i].Value.Scale.X, realInstanceList[i].Value.Scale.Y, realInstanceList[i].Value.Scale.Z);
                }

                streamWriter.Flush();
                streamWriter.Close();
                outFileStream.Close();
            }

            Console.WriteLine("[Mesh Statistic] Tri: {0}, Total Submesh: {1}, Individual: {2} Material: {3}", statistic.triCount, meshCount, instanceLocationList.Count, materialCount);
        }

        private static float CalcLocalPose(Vector3[] Vertice, out Matrix4x4 worldMat, out float DotResult)
        {
            var ref1_1 = Vertice[0];
            var ref1_2 = Vertice[1];
            var ref1_3 = Vertice[2];
            
            var side1 = Vector3.Normalize(ref1_2 - ref1_1);
            var side2 = Vector3.Normalize(ref1_3 - ref1_1);

            DotResult = Vector3.Dot(side1, side2);

            if (DotResult > 0.99f)
            {
                ref1_1 = Vertice[1];
                ref1_2 = Vertice[2];
                ref1_3 = Vertice[3];
            
                side1 = Vector3.Normalize(ref1_2 - ref1_1);
                side2 = Vector3.Normalize(ref1_3 - ref1_1);
                DotResult = Vector3.Dot(side1, side2);
                if (DotResult > 0.99f)
                {
                    ref1_1 = Vertice[2];
                    ref1_2 = Vertice[3];
                    ref1_3 = Vertice[4];
            
                    side1 = Vector3.Normalize(ref1_2 - ref1_1);
                    side2 = Vector3.Normalize(ref1_3 - ref1_1);
                    DotResult = Vector3.Dot(side1, side2);
                    if (DotResult > 0.99f)
                    {
                        Console.WriteLine("may loss angle: {0} - {1}", Vertice.Length, DotResult);
                    }
                }
            }

            var up = Vector3.Cross(side1, side2);
            up = Vector3.Normalize(up);
            var front = Vector3.Cross(side1, up);
            front = Vector3.Normalize(front);

            worldMat = Matrix4x4.CreateWorld(ref1_1, front, up);
            return (ref1_2 - ref1_1).Length();;
        }
    }
}