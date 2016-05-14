﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Media;
using System.IO;

namespace FaceMelody.SystemCore
{
    class SoundTools
    {
        const int _chunkID = 1179011410;
        const int _riffType = 1163280727;
        const int _fmtID = 544501094;
        const int _fmtSize = 16;//限制文件大小60分钟！
        const int _dataID = 1635017060;
        const Int16 _fmtCode = 1;
        const Int16 _fmtBlockAlign = 4;

        /// <summary>
        /// 基本声音类，包含声音数组长度，左右声道数组内容
        /// </summary>
        public struct BaseSound
        {
            /// <summary>
            /// 左声道数据（单声道时为主声道数据）
            /// </summary>
            public List<float> LVoice;
            /// <summary>
            /// 右声道数据（单声道时为null）
            /// </summary>
            public List<float> RVoice;
            /// <summary>
            /// 采样率
            /// </summary>
            public int SampleRate;
            /// <summary>
            /// 比特深度，勿改
            /// </summary>
            public const int BitDepth = 16;
            /// <summary>
            /// 清空本段声音
            /// </summary>
            public void clear()
            {
                LVoice = null;
                RVoice = null;
            }
        }

        /// <summary>
        /// <para>读取一个wav格式音频，返回数组流</para>
        /// <para>读取失败则length为0</para>
        /// <para>单声道则RVoice为null</para>
        /// </summary>
        /// <param name="file">包含文件名的完整路径</param>
        /// <returns></returns>
        public BaseSound sound_reader(string file)
        {
            BaseSound ret = new BaseSound();
            float[] L,R;
            int sample_rate = 0;
            if (readWav(file, out L, out R, ref sample_rate))
            {
                ret.SampleRate = sample_rate;
                ret.LVoice = L.ToList<float>();
                if (R != null)
                    ret.RVoice = R.ToList<float>();
                else
                    ret.RVoice = null;
            }
            return ret;
        }

        /// <summary>
        /// 将BaseSound写入wav文件
        /// </summary>
        /// <param name="sound">要写入的声音</param>
        /// <param name="file">包含文件名的完整路径</param>
        /// <returns></returns>
        public bool sound_writer(BaseSound sound, string file)
        {
            try
            {
                if (File.Exists(file))
                    File.Delete(file);
                FileStream fs = new FileStream(file, FileMode.Create);
                BinaryWriter bw = new BinaryWriter(fs);
                bool double_wave = (sound.RVoice != null);
                int bytes = ((double_wave) ? (2) : (1)) * sound.LVoice.Count * BaseSound.BitDepth / 8;
                //chunk 1
                bw.Write(_chunkID);
                bw.Write(9 * 4 + bytes);
                bw.Write(_riffType);
                //chunk 2
                bw.Write(_fmtID);
                bw.Write(_fmtSize);
                bw.Write(_fmtCode);
                bw.Write((double_wave ? ((short)2) : ((short)1)));
                bw.Write(sound.SampleRate);
                bw.Write(sound.SampleRate * (double_wave ? (2) : (1)) * BaseSound.BitDepth / 8);
                bw.Write(_fmtBlockAlign);
                bw.Write((short)BaseSound.BitDepth);
                //chunk 3
                bw.Write(_dataID);
                bw.Write(bytes);

                if (double_wave)
                {
                    for (int i = 0; i < bytes / 2; i++)
                    {
                        Int16 to_save = (Int16)(sound.LVoice[i] * Int16.MaxValue);
                        byte[] s1 = BitConverter.GetBytes(to_save);
                        bw.Write(s1);
                        to_save = (Int16)(sound.RVoice[i] * Int16.MaxValue);
                        byte[] s2 = BitConverter.GetBytes(to_save);
                        bw.Write(s2);
                    }
                }
                else
                {
                    for (int i = 0; i < bytes; i++)
                    {
                        Int16 to_save = (Int16)(sound.LVoice[i] * Int16.MaxValue);
                        byte[] s1 = BitConverter.GetBytes(to_save);
                        bw.Write(s1);
                    }
                }

                fs.Flush();
                bw.Close();
                fs.Close();
                return true;
            }
            catch
            {
                return false;
            }
        }

        private bool readWav(string filename, out float[] L, out float[] R,ref int sample_rate)
        {
            L = R = null;
            //float [] left = new float[1];

            //float [] right;
            try
            {
                FileStream fs = File.Open(filename, FileMode.Open);
                BinaryReader reader = new BinaryReader(fs);

                // chunk 0
                int chunkID = reader.ReadInt32();
                int fileSize = reader.ReadInt32();//从下一个变量开始所占的大小，共计9*4+bytes比特
                int riffType = reader.ReadInt32();


                // chunk 1
                int fmtID = reader.ReadInt32();
                int fmtSize = reader.ReadInt32(); // bytes for this chunk
                int fmtCode = reader.ReadInt16();
                int channels = reader.ReadInt16();
                int sampleRate = reader.ReadInt32();
                int byteRate = reader.ReadInt32(); // = sampleRate * channels * bitDepth / 8
                int fmtBlockAlign = reader.ReadInt16();
                int bitDepth = reader.ReadInt16();

                sample_rate = sampleRate;

                if (fmtSize == 18)
                {
                    // Read any extra values
                    int fmtExtraSize = reader.ReadInt16();
                    reader.ReadBytes(fmtExtraSize);
                }

                // chunk 2
                int dataID = reader.ReadInt32();
                int bytes = reader.ReadInt32();

                // DATA!
                byte[] byteArray = reader.ReadBytes(bytes);
                reader.Close();
                fs.Close();
                reader.Dispose();
                fs.Dispose();
                int bytesForSamp = bitDepth / 8;
                int samps = bytes / bytesForSamp;


                float[] asFloat = null;
                switch (bitDepth)
                {
                    case 64:
                        double[] asDouble = new double[samps];
                        Buffer.BlockCopy(byteArray, 0, asDouble, 0, bytes);
                        asFloat = Array.ConvertAll(asDouble, e => (float)e);
                        asDouble = null;
                        break;
                    case 32:
                        asFloat = new float[samps];
                        Buffer.BlockCopy(byteArray, 0, asFloat, 0, bytes);
                        break;
                    case 16:
                        Int16[] asInt16 = new Int16[samps];
                        Buffer.BlockCopy(byteArray, 0, asInt16, 0, bytes);
                        asFloat = Array.ConvertAll(asInt16, e => e / (float)Int16.MaxValue);
                        asInt16 = null;
                        break;
                    default:
                        return false;
                }

                switch (channels)
                {
                    case 1:
                        L = asFloat;
                        R = null;
                        asFloat = null;
                        byteArray = null;
                        return true;
                    case 2:
                        L = new float[samps / 2];
                        R = new float[samps / 2];
                        for (int i = 0, s = 0; i < samps / 2; i++)
                        {
                            L[i] = asFloat[s++];
                            R[i] = asFloat[s++];
                        }
                        asFloat = null;
                        byteArray = null;
                        return true;
                    default:
                        asFloat = null;
                        byteArray = null;
                        return false;
                }
            }
            catch
            {
                //Debug.Log("...Failed to load note: " + filename);
                return false;
                //left = new float[ 1 ]{ 0f };
            }
        }
    }
}
