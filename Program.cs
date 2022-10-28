// a program to trans id-deamon's ascii custom model file to smd file format
// with external instance & material infos

using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace smdconv
{
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

            String DstMeshFile = Path.GetFileNameWithoutExtension(SrcMeshFileRoot);
            DstMeshFile = DstMeshFile + ".smd";

            DstMeshFile = Path.Join(Path.GetDirectoryName(SrcMeshFileRoot), DstMeshFile);
            Console.WriteLine("Converting {0} -> {1}", SrcMeshFileRoot, DstMeshFile);


            List<string> existToken = new List<string>();
            Dictionary<string, string> existMaterialToken = new Dictionary<string, string>();


            int materialCount = 0;
            int meshCount = 0;
            
            
            
            // instance transform file ( submesh id -> transform )
            
            // material texture file
            
            
            // basic smd file

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

                    string[] Vertice = new string[VertexCount];
                    string[] Normals = new string[VertexCount];
                    string[] TexCoord0 = new string[VertexCount];

                    for (int v = 0; v < VertexCount; ++v)
                    {
                        line = streamReader.ReadLine();
                        Vertice[v] = line;
                        
                        line = streamReader.ReadLine();
                        Normals[v] = line;   
                        
                        line = streamReader.ReadLine();
                        if(TexcoordCount > 0)
                        {
                            for (int tc = 0; tc < TexcoordCount; ++tc)
                            {
                                line = streamReader.ReadLine();
                                if (tc == 0)
                                {
                                    TexCoord0[v] = line;
                                }
                            }
                        }
                        else
                        {
                            TexCoord0[v] = "0.0 0.0";
                        }
                    }
                    
                    // tri
                    line = streamReader.ReadLine();
                    int TriCount = Int32.Parse(line);

                    string token = String.Format("{0}_{1}", VertexCount, TriCount);

                    bool writeSubmesh = true;
                    
                    if (SkipInstance)
                    {
                        if (existToken.Contains(token))
                        {
                            writeSubmesh = false;
                        }
                        else
                        {
                            existToken.Add(token);
                        }
                    }

                    if (writeSubmesh)
                    {
                        streamWriter.WriteLine("triangles");
                        statistic.triCount += TriCount;
                        statistic.meshCount++;
                    }
                    
                    // 用第一个三角面，来恢复transform，做法：
                    // tri1，以第一个顶点为origin，构造一个齐次坐标系M1
                    // tri2，以第一个顶点为origin，构造一个齐次坐标系M2
                    // M = M2 * invM1
                    
                    // tri1，先将他归还到原点
                    for (int t = 0; t < TriCount; ++t)
                    {
                        line = streamReader.ReadLine();

                        if (writeSubmesh)
                        {
                            var triIdx = line.Split(' ');

                            streamWriter.WriteLine(materialName);

                            streamWriter.WriteLine(String.Format("0  {0}  {1}  {2}", Vertice[Int32.Parse(triIdx[0])],
                                Normals[Int32.Parse(triIdx[0])], TexCoord0[Int32.Parse(triIdx[0])]));
                            streamWriter.WriteLine(String.Format("0  {0}  {1}  {2}", Vertice[Int32.Parse(triIdx[2])],
                                Normals[Int32.Parse(triIdx[2])], TexCoord0[Int32.Parse(triIdx[2])]));
                            streamWriter.WriteLine(String.Format("0  {0}  {1}  {2}", Vertice[Int32.Parse(triIdx[1])],
                                Normals[Int32.Parse(triIdx[1])], TexCoord0[Int32.Parse(triIdx[1])]));
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
            
            Console.WriteLine("[Mesh Statistic] Tri: {0}, Total Submesh: {1}, Individual: {2} Material: {3}", statistic.triCount, meshCount, statistic.meshCount, materialCount);
        }
    }
}