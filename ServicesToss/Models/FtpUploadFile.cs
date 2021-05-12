using DataBaseConnection;
using NPT_System.Shared;
using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

namespace CreditCardFormsControls
{
    class FtpUploadFile
    {
        public bool result { get; set; }
        public string message { get; set; }

        public FtpUploadFile FtpUpFile(Image img, FileInfo imgfile, string upfilename)
        {
            FtpUploadFile ff = new FtpUploadFile();
            string f_year = "", f_month = "";
            using (SqlServerDataConnection sqlCon = new SqlServerDataConnection())
            {
                sqlCon.CommandString = "Select (CONVERT([varchar],datepart(year,GetDate()),(0))) As year_txt,(right('00'+CONVERT([varchar],datepart(month,GetDate()),(0)),(2))) As month_txt";
                DataTable dt = sqlCon.ExecuteQuery();
                if (dt.Rows.Count > 0)
                {
                    f_year = dt.Rows[0]["year_txt"].ToString().Trim();
                    f_month = dt.Rows[0]["month_txt"].ToString().Trim();
                }
            }

            string userName = "fileshare01@thiensurat.com";
            string passWord = "CX8Q2Z7wO";
            string MParth = "ftp://ftp.thiensurat.com/ccfc";
            string DirPath = MParth + "/" + f_year;

            if (CreateFTPDirectory(DirPath, userName, passWord))
            {
                string subDirPath = DirPath + "/" + f_month;

                if (CreateFTPDirectory(subDirPath, userName, passWord))
                {
                    string upPath = subDirPath + "/" + upfilename + ".jpg";
                    if (FtpFileExists(upPath, userName, passWord))
                    {
                        ff.result = true;
                        ff.message = "File found.";
                    }
                    else
                    {
                        try
                        {
                            FtpWebRequest request = (FtpWebRequest)WebRequest.Create(upPath);
                            request.Credentials = new NetworkCredential(userName, passWord);

                            request.KeepAlive = true;
                            request.UseBinary = true;
                            request.Method = WebRequestMethods.Ftp.UploadFile;
                            double fileSizeKB = imgfile.Length / 1024;
                            Image image = img;
                            //if (fileSizeKB > 200)
                            {
                                Bitmap newImage = ResizeImg(img, (img.Width / 2), (img.Height / 2));
                                image = newImage;
                            }

                            MemoryStream ms = new MemoryStream();
                            image.Save(ms, ImageFormat.Jpeg);
                            byte[] buffer = ms.ToArray();

                            request.ContentLength = ms.Length;

                            Stream ftpstream = request.GetRequestStream();
                            ftpstream.Write(buffer, 0, buffer.Length);
                            ftpstream.Close();

                            FtpWebResponse response = (FtpWebResponse)request.GetResponse();

                            ff.message = string.Format("Upload File Complete, status {0}", response.StatusDescription);
                            //Console.WriteLine("Upload File Complete, status {0}", response.StatusDescription);

                            response.Close();

                            ms.Close();
                            ff.result = true;
                        }
                        catch (Exception ex)
                        {
                            ff.result = false;
                            ff.message = ex.Message + "//" + upPath;
                        }
                    }
                }
            }

            img.Dispose();

            return ff;
        }

        private Bitmap ResizeImg(Image srcImage, int newWidth, int newHeight)
        {
            Bitmap newImage = new Bitmap(newWidth, newHeight, PixelFormat.Format16bppRgb555);
            using (Graphics gr = Graphics.FromImage(newImage))
            {
                gr.SmoothingMode = SmoothingMode.HighQuality;
                gr.InterpolationMode = InterpolationMode.HighQualityBicubic;
                gr.PixelOffsetMode = PixelOffsetMode.HighQuality;
                gr.DrawImage(srcImage, new Rectangle(0, 0, newWidth, newHeight));
            }
            return newImage;
        }

        private bool CreateFTPDirectory(string directory, string ftpUser, string ftpPassword)
        {

            try
            {
                //create the directory
                FtpWebRequest requestDir = (FtpWebRequest)FtpWebRequest.Create(new Uri(directory));
                requestDir.Method = WebRequestMethods.Ftp.MakeDirectory;
                requestDir.Credentials = new NetworkCredential(ftpUser, ftpPassword);
                requestDir.UsePassive = true;
                requestDir.UseBinary = true;
                requestDir.KeepAlive = false;
                FtpWebResponse response = (FtpWebResponse)requestDir.GetResponse();
                Stream ftpStream = response.GetResponseStream();

                ftpStream.Close();
                response.Close();

                return true;
            }
            catch (WebException ex)
            {
                FtpWebResponse response = (FtpWebResponse)ex.Response;
                if (response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
                {
                    response.Close();
                    return true;
                }
                else
                {
                    response.Close();
                    return false;
                }
            }
        }

        private bool FtpFileExists(string directoryPath, string ftpUser, string ftpPassword)
        {
            bool IsExists = true;
            try
            {
                FtpWebRequest request = (FtpWebRequest)WebRequest.Create(directoryPath);
                request.Credentials = new NetworkCredential(ftpUser, ftpPassword);
                request.Method = WebRequestMethods.Ftp.GetFileSize;

                FtpWebResponse response = (FtpWebResponse)request.GetResponse();
                response.Close();
            }
            catch (WebException ex)
            {
                IsExists = false;
            }
            return IsExists;
        }
    }
}
