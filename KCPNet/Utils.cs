using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;

namespace KCPNet
{
    public static class Utils
    {
        public static readonly DateTime utcStart = new DateTime(1970, 1, 1);

        public static ulong GetUTCStartMilliseconds()
        {
            TimeSpan ts = DateTime.UtcNow - utcStart;
            return (ulong) ts.TotalMilliseconds;
        }
        
        // public static byte[] Serialize(object msg)
        // {
        //     using (MemoryStream ms = new MemoryStream())
        //     {
        //         BinaryFormatter bf = new BinaryFormatter();
        //         bf.Serialize(ms, msg);
        //         ms.Seek(0, SeekOrigin.Begin);
        //         return ms.ToArray();
        //     }
        // }
        //
        // public static T DeSerialize<T>(byte[] bytes)
        // {
        //     using (MemoryStream ms = new MemoryStream(bytes))
        //     {
        //         BinaryFormatter bf = new BinaryFormatter();
        //         T msg = (T) bf.Deserialize(ms);
        //         return msg;
        //     }
        // }

        public static byte[] Compress(byte[] input)
        {
            using (MemoryStream outMS = new MemoryStream())
            {
                using (GZipStream gzs = new GZipStream(outMS, CompressionMode.Compress, true))
                {
                    gzs.Write(input, 0, input.Length);
                    gzs.Close();
                    return outMS.ToArray();
                }
            }
        }

        public static byte[] DeCompress(byte[] input)
        {
            using (MemoryStream inputMS = new MemoryStream(input))
            {
                using (MemoryStream outMS = new MemoryStream())
                {
                    using (GZipStream gzs = new GZipStream(outMS, CompressionMode.Decompress))
                    {
                        byte[] bytes = new byte[1024];
                        int len = 0;
                        while ((len = gzs.Read(bytes, 0, bytes.Length)) > 0)
                        {
                            outMS.Write(bytes, 0, len);
                        }
                        gzs.Close();
                        return outMS.ToArray();
                    }
                }
                
            }
        }
    }
}