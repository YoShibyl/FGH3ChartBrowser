using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using F = System.IO.File;

// Shamelessly stolen from FastGH3's source code lol
// Credit to donnaken15: https://github.com/donnaken15/FastGH3/blob/d3e41782d55b61af10619c44c40e85e8a001f613/SOURCE/FastGH3/Sng.cs
// (I'm super sorry)

namespace SngParser
{
    public struct Sng
    {
        public uint version;
        public byte[] xorMask;
        public Dictionary<string, string> meta;
        public List<File> files;
        public struct File
        {
            public string name;
            public byte[] data;
        }

        static string readstr(BinaryReader br)
        {
            return new string(br.ReadChars((int)br.ReadUInt32()));
        }
        public static Sng Load(string fname)
        {
            Stream f = F.OpenRead(fname);
            BinaryReader br = new BinaryReader(f, System.Text.Encoding.UTF8);
            Sng sng = new Sng();
            string magic = new string(br.ReadChars(6));
            if (magic != "SNGPKG")
            {
                throw new Exception(magic);
            }
            sng.version = br.ReadUInt32();
            sng.xorMask = br.ReadBytes(16);
            ulong metasize = br.ReadUInt64();
            long test = f.Position;
            ulong metacount = br.ReadUInt64();
            sng.meta = new Dictionary<string, string>();
            for (ulong i = 0; i < metacount; i++)
                sng.meta.Add(readstr(br), readstr(br));

            ulong idxsize = br.ReadUInt64();
            test = f.Position;
            ulong fcount = br.ReadUInt64();
            sng.files = new List<File>();
            for (ulong i = 0; i < fcount; i++)
            {
                byte fnamelen = br.ReadByte();
                string name = new string(br.ReadChars(fnamelen));
                ulong fsize = br.ReadUInt64();
                ulong index = br.ReadUInt64();
                long oldpos = f.Position;
                f.Position = (long)index;
                byte[] data = br.ReadBytes((int)fsize);
                for (int x = 0; x < (int)fsize; x++)
                    data[x] ^= (byte)(sng.xorMask[x & 0xF] ^ ((byte)x));
                f.Position = oldpos;
                sng.files.Add(new File()
                {
                    data = data,
                    name = name
                });
            }
            ulong concatsize = br.ReadUInt64();
            f.Close();
            br.Dispose();
            return sng;
        }
    }
}