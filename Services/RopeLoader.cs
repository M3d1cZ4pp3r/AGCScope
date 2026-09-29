using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace AGCScope.Services
{
    public class RopeLoader
    {
        public static RopeImage Load(string path)
        {
            if(new FileInfo(path).Length != 2 * RopeImage.Banks * RopeImage.WordsPerBank)
            {
                throw new InvalidDataException("Unexpected AGC rope image size.");
            }

            RopeImage memory = new RopeImage();

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);

            for (int fileBank = 0; fileBank < RopeImage.Banks; fileBank++)
            {
                for (int fileWord = 0; fileWord < RopeImage.WordsPerBank; fileWord++)
                {
                    ushort high = br.ReadByte();
                    ushort low = br.ReadByte();

                    // Big-endian
                    ushort raw = (ushort)((high << 8) | low);

                    // words are stored shifted 1 to the left
                    ushort word = (ushort)((raw >> 1) & 0x7FFF);

                    // Banks are stored in a slightly changed order
                    memory[FileBankToAgcBank(fileBank), fileWord] = word;
                }
            }



            return memory;
        }

        static int FileBankToAgcBank(int fileBank)
        {
            return fileBank switch
            {
                0 => 2,
                1 => 3,
                2 => 0,
                3 => 1,
                _ => fileBank
            };
        }
    }
}
