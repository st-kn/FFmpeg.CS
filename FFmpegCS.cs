using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Drawing;
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;

namespace FFmpegCS
{
    public class FFmpeg
    {

        private static string FFmpegLastOutputString;

        public static bool GetVideoThumbnail(string srcPath, string destPath, int seconds, Size thumbSize, int timeoutms)
        {
            string args = string.Format("-ss {0} -i \"{1}\" -s {2}x{3} -f image2 -vframes 1 -y \"{4}\"", seconds, srcPath, thumbSize.Width, thumbSize.Height, destPath);

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = "ffmpeg.exe";
            info.Arguments = args;
            info.CreateNoWindow = true;
            info.UseShellExecute = false;
            info.RedirectStandardError = true;

            File.Delete(destPath);

            using (Process p = new Process())
            {
                p.StartInfo = info;
                p.Start();
                p.WaitForExit(timeoutms);
                FFmpegLastOutputString = p.StandardError.ReadToEnd();
            }



            return File.Exists(destPath);
        }

        public static bool GetVideoThumbnail(string srcPath, string destPath, int seconds, Size thumbSize)
        {
            return GetVideoThumbnail(srcPath, destPath, seconds, thumbSize, 5000);
        }

        public static Image GetVideoThumbnail(string srcPath, int seconds, Size thumbSize, int timeoutms)
        {
            string tmpPath = "tmp/tmp.jpg";
            if (!Directory.Exists("tmp")) Directory.CreateDirectory("tmp");

            if (GetVideoThumbnail(srcPath, tmpPath, seconds, thumbSize, timeoutms))
            {
                try
                {
                    FileStream fs = new FileStream(tmpPath, FileMode.Open, FileAccess.Read);
                    Image img = Image.FromStream(fs);
                    fs.Close();
                    return img;
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    return null;
                }
            }
            else
            {
                return null;
            }
        }

        public static Image GetVideoThumbnail(string srcPath, int seconds, Size thumbSize)
        {
            return GetVideoThumbnail(srcPath, seconds, thumbSize, 5000);
        }

        public static string GetFFmpegOutput()
        {
            return FFmpegLastOutputString;
        }

        public static VideoInfomation GetVideoInfomation(string srcPath, int timeoutms)
        {
            string args = string.Format(" -i \"{0}\"", srcPath);

            ProcessStartInfo info = new ProcessStartInfo();
            info.FileName = "ffmpeg.exe";
            info.Arguments = args;
            info.CreateNoWindow = true;
            info.UseShellExecute = false;
            info.RedirectStandardError = true;


            using (Process p = new Process())
            {
                p.StartInfo = info;
                p.Start();
                p.WaitForExit(timeoutms);
                FFmpegLastOutputString = p.StandardError.ReadToEnd();
                return VideoInfomationParser(FFmpegLastOutputString);
            }

        }

        public static VideoInfomation GetVideoInfomation()
        {
            return VideoInfomationParser(FFmpegLastOutputString);
        }

        private static VideoInfomation VideoInfomationParser(string ffmpegOutput)
        {
            VideoInfomation info = new VideoInfomation();

            //Console.WriteLine(ffmpegOutput);


            Regex reDuration = new Regex(@"[Dd]uration:\s([\d]+?):([\d]+?):([\d]+?)\.([\d]+?)[\D]");
            Match mDuration = reDuration.Match(ffmpegOutput);
            if (mDuration.Success)
            {
                int durationHour = int.Parse(mDuration.Groups[1].Value);
                int durationMin = int.Parse(mDuration.Groups[2].Value);
                int durationSec = int.Parse(mDuration.Groups[3].Value);
                int durationMSec = int.Parse(mDuration.Groups[4].Value) * 10;

                long durationms = (((durationHour * 60) + durationMin) * 60 + durationSec) * 1000 + durationMSec;

                info.Duration = durationms / 1000;
                info.DurationMs = durationms;
                info.DurationHour = durationHour;
                info.DurationMinute = durationMin;
                info.DurationSecond = durationSec;
                info.DurationMilliSecond = durationMSec;

            }

            Regex reStreamVideoCodecSize = new Regex(@"Stream\s#([\d]+?:[\d]+?).*?:\sVideo:\s([^\s,]+).*?,\s.*?\s([1-9][\d]*?)x([1-9][\d]*?)[^\d]");
            Match mStreamVideoCodecSize = reStreamVideoCodecSize.Match(ffmpegOutput);
            if (mStreamVideoCodecSize.Success)
            {
                string codec = mStreamVideoCodecSize.Groups[2].Value;
                int width = int.Parse(mStreamVideoCodecSize.Groups[3].Value);
                int height = int.Parse(mStreamVideoCodecSize.Groups[4].Value);
                info.VideoCodec = codec;
                info.VideoWidth = width;
                info.VideoHeight = height;

                string streamId = mStreamVideoCodecSize.Groups[1].Value;

                Regex reStreamVideoBitrate = new Regex(@"Stream\s#" + streamId + @".*?:\sVideo:\s.*?\s([\d]+?)\skb\/s");
                Match mStreamVideoBitrate = reStreamVideoBitrate.Match(ffmpegOutput);
                if (mStreamVideoBitrate.Success)
                {
                    int bitrate = int.Parse(mStreamVideoBitrate.Groups[1].Value);
                    info.VideoBitrateKB = bitrate;
                }

                Regex reStreamVideoFramerate = new Regex(@"Stream\s#" + streamId + @".*?:\sVideo:\s.*?\s([\d]+?\.?[\d]+?)\sfps");
                Match mStreamVideoFramerate = reStreamVideoFramerate.Match(ffmpegOutput);
                if (mStreamVideoFramerate.Success)
                {
                    double framerate = double.Parse(mStreamVideoFramerate.Groups[1].Value);
                    info.VideoFramerate = framerate;
                }
                else
                {
                    Regex reStreamVideoFramerateTbr = new Regex(@"Stream\s#" + streamId + @".*?:\sVideo:\s.*?\s([\d]+?\.?[\d]+?)\stbr");
                    Match mStreamVideoFramerateTbr = reStreamVideoFramerateTbr.Match(ffmpegOutput);
                    if (mStreamVideoFramerateTbr.Success)
                    {
                        double framerate = double.Parse(mStreamVideoFramerateTbr.Groups[1].Value);
                        info.VideoFramerate = framerate;
                    }
                }
            }

            Regex reStreamAudio = new Regex(@"Stream\s#([\d]+?:[\d]+?).*?:\sAudio:\s(.+?)\s.*?\s([\d]+?)\sHz.*?\s([\d]+?)\skb\/s");
            Match mStreamAudio = reStreamAudio.Match(ffmpegOutput);
            if (mStreamAudio.Success)
            {
                string codec = mStreamAudio.Groups[2].Value;
                int samplingrate = int.Parse(mStreamAudio.Groups[3].Value);
                int bitrate = int.Parse(mStreamAudio.Groups[4].Value);

                info.AudioCodec = codec;
                info.AudioSamplingrate = samplingrate;
                info.AudioBitrate = bitrate;
            }

            /*
            Console.WriteLine("Duration: " + info.DurationMs + "[ms]");
            Console.WriteLine("VideoCodec: " + info.VideoCodec);
            Console.WriteLine("VideoSize: " + info.VideoWidth + " x " + info.VideoHeight);
            Console.WriteLine("VideoBitrate: " + info.VideoBitrateKB + "kb/s");
            Console.WriteLine("VideoFramerate: " + info.VideoFramerate.ToString("F2") + "fps");
            Console.WriteLine("AudioCodec: " + info.AudioCodec);
            Console.WriteLine("AudioSamplingrae: " + info.AudioSamplingrate);
            Console.WriteLine("AudioBitrate: " + info.AudioBitrate + "kb/s");
            */

            return info;

        }

    }

    public struct VideoInfomation
    {
        public long Duration;
        public long DurationMs;
        public int DurationHour;
        public int DurationMinute;
        public int DurationSecond;
        public int DurationMilliSecond;

        public string VideoCodec;
        public int VideoWidth;
        public int VideoHeight;
        public int VideoBitrateKB;
        public double VideoFramerate;

        public string AudioCodec;
        public int AudioSamplingrate;
        public int AudioBitrate;

    }
}
