﻿// a program to trans id-deamon's ascii custom model file to smd file format
// with external instance & material infos

using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;


    
namespace smdconv
{
    public static class QuatExtensions
    {
        const float FLT_EPSILON = 1.192092896e-07F;
        const double M_SQRT2 = 1.41421356237309504880;
        public static double hypotf(float x, float y)
        {
            double p, qp;
            p = Math.Max(x,y);
            if(p==0) return 0;
            qp = Math.Min(y,x) / p;    
            return p * Math.Sqrt(1.0 + qp*qp);
        }
            
        static void quat_to_mat3_no_error(Quaternion q, out Matrix4x4 m )
        {
            double q0, q1, q2, q3, qda, qdb, qdc, qaa, qab, qac, qbb, qbc, qcc;

            q0 = M_SQRT2 * (double)q.W;
            q1 = M_SQRT2 * (double)q.X;
            q2 = M_SQRT2 * (double)q.Y;
            q3 = M_SQRT2 * (double)q.Z;

            qda = q0 * q1;
            qdb = q0 * q2;
            qdc = q0 * q3;
            qaa = q1 * q1;
            qab = q1 * q2;
            qac = q1 * q3;
            qbb = q2 * q2;
            qbc = q2 * q3;
            qcc = q3 * q3;

            m.M11 = (float)(1.0 - qbb - qcc);
            m.M12 = (float)(qdc + qab);
            m.M13 = (float)(-qdb + qac);
            m.M14 = 0.0f;

            m.M21 = (float)(-qdc + qab);
            m.M22 = (float)(1.0 - qaa - qcc);
            m.M23 = (float)(qda + qbc);
            m.M24 = 0.0f;

            m.M31 = (float)(qdb + qac);
            m.M32 = (float)(-qda + qbc);
            m.M33 = (float)(1.0 - qaa - qbb);
            m.M34 = 0.0f;
            
            m.M41 = 0.0f;
            m.M42 = 0.0f;
            m.M43 = 0.0f;
            m.M44 = 1.0f;
        }
        
        public static void MatrixDecomposeYawPitchRoll(
        Matrix4x4 mat,
        out Vector3 euler)
        {
            double cy = hypotf(mat.M11, mat.M12);
            const float RAD_TO_DEG = (float)(180 / Math.PI);
            
            // from Blender, final correct
            if (cy > 16.0f * FLT_EPSILON)
            {
                var euler1 = new Vector3();
                {
                    euler1.X = (float) Math.Atan2(mat.M23, mat.M33);
                    euler1.Y = (float) Math.Atan2(-mat.M13, cy);
                    euler1.Z = (float) Math.Atan2(mat.M12, mat.M11);
                }
                var euler2 = new Vector3();
                {
                    euler2.X = (float)Math.Atan2(-mat.M23, -mat.M33);
                    euler2.Y = (float)Math.Atan2(-mat.M13, -cy);
                    euler2.Z = (float)Math.Atan2(-mat.M12, -mat.M11);
                }

                if (Math.Abs(euler1.X) + Math.Abs(euler1.Y) + Math.Abs(euler1.Z) >
                    Math.Abs(euler2.X) + Math.Abs(euler2.Y) + Math.Abs(euler2.Z))
                {
                    euler = euler2;
                }
                else
                {
                    euler = euler1;
                }     
            }
            else
            {
                euler.X = (float)Math.Atan2(-mat.M32, mat.M22);
                euler.Y = (float)Math.Atan2(-mat.M13, cy);
                euler.Z = 0;
            }
 
            
            euler.X *= RAD_TO_DEG;
            euler.Y *= RAD_TO_DEG;
            euler.Z *= RAD_TO_DEG;
        }
        
        public static Vector3 ToEulerAngles(this Quaternion q)
        {
            Vector3 ret;

            var rotMat = new Matrix4x4();
            quat_to_mat3_no_error( Quaternion.Normalize(q), out rotMat );

            // Matrix4x4.

            //q = Quaternion.Normalize(q);
            MatrixDecomposeYawPitchRoll(rotMat, out ret);
   
            // from ue
            // float SingularityTest = q.Z * q.X - q.W * q.Y;
            // float YawY = 2.0f * (q.W * q.Z + q.X * q.Y);
            // float YawX = (1.0f - 2.0f * (q.Y * q.Y + q.Z * q.Z));
            // float RAD_TO_DEG = (float)(180 / Math.PI);
            // //
            // ret.X = (float)(Math.Asin(2.0f * SingularityTest) * RAD_TO_DEG); // Pitch
            // ret.Y = (float)(Math.Atan2(YawY, YawX) * RAD_TO_DEG);  // Yaw
            // ret.Z = (float)(Math.Atan2(-2.0f * (q.W*q.X + q.Y*q.Z), (1.0f - 2.0f * (q.X * q.X + q.Y*q.Y))) * RAD_TO_DEG);       //Roll

            // ret.Y =   (float)((180 / Math.PI) * Math.Atan2(2f * q.X * q.W + 2f * q.Y * q.Z, 1 - 2f * (q.Z*q.Z  + q.W * q.W)));     // Yaw 
            // ret.X = (float)((180 / Math.PI) * Math.Asin(2f * ( q.X * q.Z - q.W * q.Y ) ));                             // Pitch 
            // ret.Z = (float)((180 / Math.PI) * Math.Atan2(2f * q.X * q.Y + 2f * q.Z * q.W, 1 - 2f * (q.Y * q.Y + q.Z * q.Z)));      // Roll 
            
            // ThreeAxisRot( -2*(q.Y*q.Z - q.W*q.X),
            //     q.W*q.W - q.X*q.X - q.Y*q.Y + q.X*q.X,
            //     2*(q.X*q.Z + q.W*q.Y),
            //     -2*(q.X*q.Y - q.W*q.Z),
            //     q.W*q.W + q.X*q.X - q.Y*q.Y - q.Z*q.Z,
            //     out ret);
            
            // ThreeAxisRot( 2*(q.X*q.Y + q.W*q.Z),
            //     q.W*q.W + q.X*q.X - q.Y*q.Y - q.Z*q.Z,
            //     -2*(q.X*q.Z - q.W*q.Y),
            //     2*(q.Y*q.Z + q.W*q.X),
            //     q.W*q.W - q.X*q.X - q.Y*q.Y + q.Z*q.Z,
            //     out ret);
            
            return ret;
        }
    }
    
   
    
    struct MeshStatic
    {
        public int triCount;
        public int meshCount;
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

            if (args.Length == 0)
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

            String[] SrcMeshFiles = args;
            String SrcMeshFileRoot = args[0];

            if (!File.Exists(SrcMeshFileRoot))
            {
                Console.WriteLine("[Error] Src File Not Exist!");
                return;
            }



            Dictionary<string, int> existToken = new Dictionary<string, int>();
            Dictionary<string, string> existMaterialToken = new Dictionary<string, string>();


            int materialCount = 0;
            int meshCount = 0;

            List<Vector3> instanceLocationList = new List<Vector3>();
            List<Quaternion> instanceRotationList = new List<Quaternion>();
            List<Vector3> instanceDirectEuler = new List<Vector3>();

            List<KeyValuePair<int, KeyValuePair<Vector3, Quaternion>>> realInstanceList =
                new List<KeyValuePair<int, KeyValuePair<Vector3, Quaternion>>>();

            // instance transform file ( submesh id -> transform )
            
            // material texture file
            
            
            // basic smd file
            String DstMeshFile = Path.GetFileNameWithoutExtension(SrcMeshFileRoot);
            DstMeshFile = DstMeshFile + ".smd";

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
            
            for( int file = 0; file < SrcMeshFiles.Length; ++file )
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
                    }
                    
                    // vertex
                    line = streamReader.ReadLine();
                    int VertexCount = Int32.Parse(line);

                    Vector3[] Vertice = new Vector3[VertexCount];
                    Vector3[] Normals = new Vector3[VertexCount];
                    Vector2[] TexCoord0 = new Vector2[VertexCount];

                    for (int v = 0; v < VertexCount; ++v)
                    {
                        line = streamReader.ReadLine();
                        var v3 = line.Split(' ');
                        Vertice[v] = new Vector3( float.Parse(v3[0]), -float.Parse(v3[2]), float.Parse(v3[1]) );
                        
                        line = streamReader.ReadLine();
                        v3 = line.Split(' ');
                        Normals[v] = new Vector3( float.Parse(v3[0]), -float.Parse(v3[2]), float.Parse(v3[1]) );
                        
                        line = streamReader.ReadLine();
                        if(TexcoordCount > 0)
                        {
                            for (int tc = 0; tc < TexcoordCount; ++tc)
                            {
                                line = streamReader.ReadLine();
                                if (tc == 0)
                                {
                                    var v2 = line.Split(' ');
                                    TexCoord0[v] = new Vector2( float.Parse(v2[0]), float.Parse(v2[1]));
                                }
                            }
                        }
                        else
                        {
                            TexCoord0[v] = Vector2.Zero;
                        }
                    }
                    
                    // tri
                    line = streamReader.ReadLine();
                    int TriCount = Int32.Parse(line);

                    string token = String.Format("{0}_{1}", VertexCount, TriCount);

                    bool writeSubmesh = true;
                    
                    // 用第一个三角面，来恢复transform，做法：
                    // tri1，以第一个顶点为origin，构造一个齐次坐标系M1
                    // tri2，以第一个顶点为origin，构造一个齐次坐标系M2
                    // M = M2 * invM1
                    
                    // tri1，先将他归还到原点
                    var ref1_1 = Vertice[0];
                    var ref1_2 = Vertice[1];
                    var ref1_3 = Vertice[2];

                    var side1 = Vector3.Normalize(ref1_2 - ref1_1);
                    var side2 = Vector3.Normalize(ref1_3 - ref1_1);

                    var up = Vector3.Cross(side1, side2);
                    up = Vector3.Normalize(up);
                    var front = Vector3.Cross(side1, up);
                    front = Vector3.Normalize(front);
                    //up = new Vector3(0, 1, 0);
                    //front = new Vector3(0, 0, -1);
                    //Quaternion rotation = Quaternion.ax

                    Matrix4x4 worldMat = Matrix4x4.CreateWorld(ref1_1, front, up);
                    Matrix4x4 worldMatInv;
                    Matrix4x4.Invert(worldMat, out worldMatInv);

                    var translation = new Vector3();
                    var rotation = new Quaternion();
                    var scale = new Vector3();
                    var directEuler = new Vector3();

                    QuatExtensions.MatrixDecomposeYawPitchRoll( worldMat, out directEuler );
                    
                    Matrix4x4.Decompose(worldMat, out scale, out rotation, out translation);
                    // Console.WriteLine("Translation: {0}",translation);
                    // Console.WriteLine("Rotation: {0}", rotation.ToEulerAngles());
                    // Console.WriteLine("Scale: {0}", scale);
                    
                    if (SkipInstance)
                    {
                        if (existToken.ContainsKey(token))
                        {
                            writeSubmesh = false;
                            realInstanceList.Add( new KeyValuePair<int, KeyValuePair<Vector3, Quaternion>>( existToken[token], new KeyValuePair<Vector3, Quaternion>(translation, rotation)) );
                        }
                        else
                        {
                            var currIdx = existToken.Count;
                            existToken.Add(token, currIdx);
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
                        instanceDirectEuler.Add(directEuler);
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
                    streamWriter.WriteLine(String.Format("{0} {1} {2} {3} {4} {5} {6}", instanceLocationList[i].X, instanceLocationList[i].Y, instanceLocationList[i].Z, instanceRotationList[i].X, instanceRotationList[i].Y, instanceRotationList[i].Z, instanceRotationList[i].W));
                }
            
                streamWriter.Flush();
                streamWriter.Close();
                outFileStream.Close();
            }

            // paster for unreal engine 5
            {
                string DstInstanceFile = Path.GetFileNameWithoutExtension(SrcMeshFileRoot) + ".smd.uelevel";
                DstInstanceFile = Path.Join(Path.GetDirectoryName(SrcMeshFileRoot), DstInstanceFile);

                File.Delete(DstInstanceFile);
                outFileStream = File.OpenWrite(DstInstanceFile);
                streamWriter = new StreamWriter(outFileStream, Encoding.UTF8);
            
                streamWriter.WriteLine("Begin Map");
                streamWriter.WriteLine("Begin Level");
                // write instance transform
                for (int i = 0; i < instanceLocationList.Count; i++)
                {
                    // if (i != 128)
                    // {
                    //     continue;
                    // }
                    streamWriter.WriteLine(String.Format("      Begin Actor Class=/Script/Engine.StaticMeshActor Name=StaticMeshActor_{0} Archetype=/Script/Engine.StaticMeshActor'/Script/Engine.Default__StaticMeshActor'",i));
                    streamWriter.WriteLine("         Begin Object Class=/Script/Engine.StaticMeshComponent Name=\"StaticMeshComponent0\" Archetype=StaticMeshComponent'/Script/Engine.Default__StaticMeshActor:StaticMeshComponent0'");
                    streamWriter.WriteLine("         End Object");
                    streamWriter.WriteLine("         Begin Object Name=\"StaticMeshComponent0\"");
                    streamWriter.WriteLine(String.Format("            StaticMesh=StaticMesh'\"/MapIsland/Assets/U4/ope-entrance-meadow-sun_0_{0}.ope-entrance-meadow-sun_0_{0}\"'",i));
                    streamWriter.WriteLine("            StaticMeshImportVersion=1");
                    streamWriter.WriteLine(String.Format("            RelativeLocation=(X={0},Y={1},Z={2})",-instanceLocationList[i].X*100,instanceLocationList[i].Y*100,instanceLocationList[i].Z*100));
                    var quatL = new Quaternion(instanceRotationList[i].X, instanceRotationList[i].Z,
                        -instanceRotationList[i].Y, instanceRotationList[i].W);
                    //var euler = instanceRotationList[i].ToEulerAngles();
                    var euler = instanceDirectEuler[i];
                    //streamWriter.WriteLine(String.Format("            RelativeRotation=(Pitch={0},Yaw={1},Roll={2})",euler.X,euler.Y,euler.Z));
                    streamWriter.WriteLine(String.Format("            RelativeRotation=(Pitch={0},Yaw={1},Roll={2})",180 - euler.Y,-euler.Z,euler.X));
                    streamWriter.WriteLine("         End Object");
                    streamWriter.WriteLine("         StaticMeshComponent=\"StaticMeshComponent0\"");
                    streamWriter.WriteLine("         RootComponent=\"StaticMeshComponent0\"");
                    streamWriter.WriteLine(String.Format("         ActorLabel=\"ope-entrance-meadow-sun_{0}\"", i));
                    streamWriter.WriteLine("      End Actor");
                }
                
                // for (int i = 0; i < realInstanceList.Count; i++)
                // {
                //     streamWriter.WriteLine(String.Format("      Begin Actor Class=/Script/Engine.StaticMeshActor Name=StaticMeshActor_{0} Archetype=/Script/Engine.StaticMeshActor'/Script/Engine.Default__StaticMeshActor'",i + instanceLocationList.Count));
                //     streamWriter.WriteLine("         Begin Object Class=/Script/Engine.StaticMeshComponent Name=\"StaticMeshComponent0\" Archetype=StaticMeshComponent'/Script/Engine.Default__StaticMeshActor:StaticMeshComponent0'");
                //     streamWriter.WriteLine("         End Object");
                //     streamWriter.WriteLine("         Begin Object Name=\"StaticMeshComponent0\"");
                //     streamWriter.WriteLine(String.Format("            StaticMesh=StaticMesh'\"/MapIsland/Assets/U4/ope-entrance-meadow-sun_0_{0}.ope-entrance-meadow-sun_0_{0}\"'",realInstanceList[i].Value));
                //     streamWriter.WriteLine("            StaticMeshImportVersion=1");
                //     streamWriter.WriteLine(String.Format("            RelativeLocation=(X={0},Y={1},Z={2})",realInstanceList[i].Key.X*100,realInstanceList[i].Key.Z*100,realInstanceList[i].Key.Y*100));
                //     streamWriter.WriteLine("         End Object");
                //     streamWriter.WriteLine("         StaticMeshComponent=\"StaticMeshComponent0\"");
                //     streamWriter.WriteLine("         RootComponent=\"StaticMeshComponent0\"");
                //     streamWriter.WriteLine(String.Format("         ActorLabel=\"ope-entrance-meadow-sun_{0}\"", i + instanceLocationList.Count));
                //     streamWriter.WriteLine("      End Actor");
                // }
                
                
                streamWriter.WriteLine("Begin Surface");
                streamWriter.WriteLine("End Surface");
                streamWriter.WriteLine("End Map");

                streamWriter.Flush();
                streamWriter.Close();
                outFileStream.Close();
            }
            
            Console.WriteLine("[Mesh Statistic] Tri: {0}, Total Submesh: {1}, Individual: {2} Material: {3}", statistic.triCount, meshCount, instanceLocationList.Count, materialCount);
        }
    }
}