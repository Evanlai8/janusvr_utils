﻿using System;
using System.Collections.Generic;

#if SYSTEM_DRAWING
using System.Drawing;
using System.Drawing.Imaging;
#endif

using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;

namespace JanusVR
{
    public class TextureUtil
    {
#if SYSTEM_DRAWING
        private static ImageFormat GetImageFormat(ImageFormatEnum format)
        {
            switch (format)
            {
                case ImageFormatEnum.JPG:
                    return ImageFormat.Jpeg;
                case ImageFormatEnum.PNG:
                default:
                    return ImageFormat.Png;
            }
        }
#endif

        public static float DecodeLightmapRGBM(float alpha, float color, Vector2 decode)
        {
            return decode.x * (float)Math.Pow(alpha, decode.y) * color;
        }

        public static Color DecodeLightmapRGBM(Color data, Vector2 decode)
        {
            float r = DecodeLightmapRGBM(data.a, data.r, decode);
            float g = DecodeLightmapRGBM(data.a, data.g, decode);
            float b = DecodeLightmapRGBM(data.a, data.b, decode);

            return new Color(r, g, b, 1);
        }

        public static Texture2D ScaleTexture(TextureExportData tex, int resolution, bool zeroAlpha = true, TextureFilterMode filterMode = TextureFilterMode.Nearest)
        {
            Texture2D texture = tex.Texture;
            // scale the texture
            Texture2D scaled = new Texture2D(resolution, resolution);

            Color[] source = texture.GetPixels();
            Color[] target = new Color[resolution * resolution];

            int scale = texture.width / resolution;
            float sca = scale * 2;

            switch (filterMode)
            {
                case TextureFilterMode.Average:
                    {
                        for (int x = 0; x < resolution; x++)
                        {
                            for (int y = 0; y < resolution; y++)
                            {
                                // sample neighbors
                                int xx = x * scale;
                                int yy = y * scale;

                                float r = 0, g = 0, b = 0, a = 0;

                                int ind = xx + (yy * texture.width);
                                for (int j = 0; j < scale; j++)
                                {
                                    Color col = source[ind + j];
                                    r += col.r;
                                    g += col.g;
                                    b += col.b;
                                    a += col.a;
                                }
                                ind = xx + ((yy + 1) * texture.width);
                                for (int j = 0; j < scale; j++)
                                {
                                    Color col = source[ind + j];
                                    r += col.r;
                                    g += col.g;
                                    b += col.b;
                                    a += col.a;
                                }

                                r = r / sca;
                                g = g / sca;
                                b = b / sca;
                                a = a / sca;
                              
                                Color sampled = new Color(r, g, b, a);
                                if (zeroAlpha)
                                {
                                    sampled.a = 1;
                                }
                                target[x + (y * resolution)] = sampled;
                            }
                        }
                    }
                    break;
                case TextureFilterMode.Nearest:
                    {
                        for (int x = 0; x < resolution; x++)
                        {
                            for (int y = 0; y < resolution; y++)
                            {
                                // sample neighbors
                                int xx = x * scale;
                                int yy = y * scale;
                                int ind = xx + (yy * texture.width);

                                Color col = source[ind];
                                if (zeroAlpha)
                                {
                                    col.a = 1;
                                }
                                target[x + (y * resolution)] = col;
                            }
                        }
                    }
                    break;
            }

            scaled.SetPixels(target);
            scaled.Apply();
            return scaled;
        }

        public static Texture2D ZeroAlpha(Texture2D input)
        {
            Texture2D output = new Texture2D(input.width, input.height);
            Color[] source = input.GetPixels();
            Color[] target = new Color[input.width * input.height];

            for (int i = 0; i < source.Length; i++)
            {
                Color s = source[i];
                s.a = 1;
                target[i] = s;
            }

            output.SetPixels(target);
            output.Apply();
            return output;
        }

        public static void ExportTexture(Texture2D input, Stream output, ImageFormatEnum imageFormat, object data, bool zeroAlpha)
        {
#if SYSTEM_DRAWING
            ImageFormat format = GetImageFormat(imageFormat);
            Color32[] colors = input.GetPixels32();
            byte[] bdata = new byte[colors.Length * 4];

            // this is slower than linear, but easier to fix the texture mirrored
            for (int x = 0; x < input.width; x++)
            {
                for (int y = 0; y < input.height; y++)
                {
                    int index = x + (y * input.width);
                    Color32 col = colors[index];

                    index = (x + ((input.height - y - 1) * input.width)) * 4;
                    //index *= 4;

                    bdata[index] = col.b;
                    bdata[index + 1] = col.g;
                    bdata[index + 2] = col.r;
                    bdata[index + 3] = col.a;
                }
            }

            Bitmap bitmap = new Bitmap(input.width, input.height);
            BitmapData locked = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

            Marshal.Copy(bdata, 0, locked.Scan0, bdata.Length);

            bitmap.UnlockBits(locked);
            bitmap.Save(output, format);

            bitmap.Dispose();
#else
            Texture2D inp;
            if (zeroAlpha)
            {
                inp = ZeroAlpha(input);
            }
            else
            {
                inp = input;
            }

            byte[] exported;
            switch (imageFormat)
            {
                case ImageFormatEnum.JPG:
                    exported = inp.EncodeToJPG((int)data);
                    break;
                default:
                case ImageFormatEnum.PNG:
                    exported = inp.EncodeToPNG();
                    break;
            }
            output.Write(exported, 0, exported.Length);

            if (zeroAlpha)
            {
                // destroy the texture
                UnityEngine.Object.DestroyImmediate(inp);
            }
#endif
        }
    }
}
