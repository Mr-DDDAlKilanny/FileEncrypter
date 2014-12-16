using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text.RegularExpressions;

namespace FileEncrypter
{
    /// <summary>
    /// Encrypt/decrypts files using Rijndael algorithm.
    /// </summary>
    /// <see cref="System.Security.Cryptography.Rijndael"/>
    /// By @ Mr.DDDAlKilanny, 2013
    /// Feel free to use the code
    internal static class Encryption
    {
        private static readonly byte[] buffer = new byte[1024 * 1024];

        private static string lastCreatedFile;

        /// <summary>
        /// Gets the last error occured during the last operation
        /// </summary>
        public static string LastError { get; private set; }

        /// <summary>
        /// Gets or sets the ReportProgressCallback. Used to report operation progress.
        /// </summary>
        [DefaultValue(null)]
        public static ReportProgressCallback ReportProgress { get; set; }

        /// <summary>
        /// Gets or sets the output directory for the encryption\decryption.
        /// Value null specifies that output directory is the same as the source file.
        /// </summary>
        public static string OutputDirectory { get; set; }

        private static bool preCheckFile(string fileName)
        {
            if (ReportProgress == null)
                throw new NullReferenceException("You must initialize ReportProgress");
            if (!File.Exists(fileName))
            {
                LastError = "File does not exist";
                return false;
            }
            LastError = null;
            return true;
        }

        private static void init(Rijndael alg, string password)
        {
            using (Rfc2898DeriveBytes deriver = new Rfc2898DeriveBytes(password,
                Encoding.ASCII.GetBytes("Mr.DDDAlKilanny")))
            {
                alg.IV = deriver.GetBytes(16);
                alg.Key = deriver.GetBytes(32);
            }
        }

        private static string getDecryptionOutputFile(string fileName, FileInfo info)
        {
            string dir = OutputDirectory == null ?
                fileName.Substring(0, fileName.LastIndexOf('\\') + 1)
                : OutputDirectory;
            return Path.Combine(dir, Path.GetFileName(info.FullName));
        }

        private static string getEncryptionOutputFile(string fileName, string password)
        {
            string dir = OutputDirectory == null ?
                fileName.Substring(0, fileName.LastIndexOf('\\') + 1)
                : OutputDirectory;
            return Path.Combine(dir, getMd5Hash(Path.GetFileName(fileName), password));
        }

        /// <summary>
        /// Encrypts the given fileName with the given password.
        /// </summary>
        /// <param name="fileName">The path of the file to be encrypted</param>
        /// <param name="password">The password to encrypt with</param>
        /// <returns>True when succeeded. When fails, false is returned,
        /// and LastError tells why.</returns>
        public static bool Encrypt(string fileName, string password)
        {
            if (!preCheckFile(fileName)) return false;
            try
            {
                encrypt(fileName, password);
                return true;
            }
            catch (Exception e)
            {
                try
                {
                    if (File.Exists(lastCreatedFile))
                        File.Delete(lastCreatedFile);
                }
                catch { }
                LastError = e.Message;
                return false;
            }
        }

        private static void writeInt(this Stream s, uint val)
        {
            for (int i = sizeof(int) * 8 - 1; i >= 0; --i)
                s.WriteByte((byte)(val >> i));
        }

        private static uint readInt(this Stream s)
        {
            uint ret = 0;
            for (int i = 0; i < sizeof(int) * 8; ++i)
                ret = (ret << 1) | (uint)s.ReadByte();
            return ret;
        }

        private static void encrypt(string fileName, string password)
        {
            lastCreatedFile = getEncryptionOutputFile(fileName, password);
            using (FileStream target = new FileStream(lastCreatedFile, FileMode.Create))
            using (Rijndael alg = Rijndael.Create())
            {
                init(alg, password);
                var info = encryptFileInfo(new FileInfo(fileName), alg);
                target.writeInt((uint)info.Length);
                target.Write(info, 0, info.Length);
                using (FileStream source = new FileStream(fileName, FileMode.Open))
                using (CryptoStream crypto = new CryptoStream(target, alg.CreateEncryptor(), CryptoStreamMode.Write))
                {
                    int tempProgress = 0, tmp;
                    while (source.Position < source.Length)
                    {
                        tmp = source.Read(buffer, 0, buffer.Length);
                        crypto.Write(buffer, 0, tmp);
                        tmp = (int)(source.Position * 100 / source.Length);
                        if (tmp != tempProgress) ReportProgress(tempProgress = tmp);
                    }
                }
            }
        }

        /// <summary>
        /// Decrypts the given fileName with the given password.
        /// </summary>
        /// <param name="fileName">The path of the file to be decrypted</param>
        /// <param name="password">The password to decrypt with</param>
        /// <returns>True when succeeded. When fails, false is returned,
        /// and LastError tells why.</returns>
        public static bool Decrypt(string fileName, string password)
        {
            if (!preCheckFile(fileName)) return false;
            try
            {
                decrypt(fileName, password);
                return true;
            }
            catch
            {
                try
                {
                    if (File.Exists(lastCreatedFile))
                        File.Delete(lastCreatedFile);
                }
                catch { }
                return false;
            }
        }

        private static void checkDecryptedFile(string fileName, FileInfo f, string password)
        {
            if (Path.GetFileName(fileName) != getMd5Hash(Path.GetFileName(f.FullName), password))
            {
                LastError = "Invalid Decrypted File Name";
                throw new Exception();
            }
        }

        private static void decrypt(string fileName, string password)
        {
            using (FileStream source = new FileStream(fileName, FileMode.Open))
            using (Rijndael alg = Rijndael.Create())
            {
                init(alg, password);
                var info = decryptFileInfo(source, alg);
                checkDecryptedFile(fileName, info, password);
                lastCreatedFile = getDecryptionOutputFile(fileName, info);
                using (CryptoStream crypto = new CryptoStream(source, alg.CreateDecryptor(), CryptoStreamMode.Read))
                using (FileStream target = new FileStream(lastCreatedFile, FileMode.Create))
                {
                    int tempProgress = 0, tmp;
                    while (source.Position < source.Length)
                    {
                        tmp = crypto.Read(buffer, 0, buffer.Length);
                        target.Write(buffer, 0, tmp);
                        tmp = (int)(source.Position * 100 / source.Length);
                        if (tmp != tempProgress) ReportProgress(tempProgress = tmp);
                    }
                }
                setFileInfo(lastCreatedFile, info);
            }
        }

        private static void setFileInfo(string fileName, FileInfo i)
        {
            File.SetAttributes(fileName, i.Attributes);
            File.SetCreationTime(fileName, i.CreationTime);
            File.SetCreationTimeUtc(fileName, i.CreationTimeUtc);
            File.SetLastAccessTime(fileName, i.LastAccessTime);
            File.SetLastAccessTimeUtc(fileName, i.LastAccessTimeUtc);
            File.SetLastWriteTime(fileName, i.LastWriteTime);
            File.SetLastWriteTimeUtc(fileName, i.LastWriteTimeUtc);
        }

        private static byte[] encryptFileInfo(FileInfo info, Rijndael alg)
        {
            string tmp = Path.GetTempFileName();
            try
            {
                using (FileStream mem = new FileStream(tmp, FileMode.Create))
                {
                    using (CryptoStream crypto = new CryptoStream(mem, alg.CreateEncryptor(), CryptoStreamMode.Write))
                    {
                        new BinaryFormatter().Serialize(crypto, info);
                    }
                }
                return File.ReadAllBytes(tmp);
            }
            finally
            {
                File.Delete(tmp);
            }
        }

        public static bool IsValidMd5Hash(string input)
        {
            return new Regex("[0-9a-f]{32}").IsMatch(input);
        }

        private static string getMd5Hash(string input, string salt)
        {
            return GetMd5Hash(GetMd5Hash(input) + salt);
        }

        public static string GetMd5Hash(string input)
        {
            MD5 md5Hash = MD5.Create();
            // Convert the input string to a byte array and compute the hash.
            byte[] data = md5Hash.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            StringBuilder sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }

        private static FileInfo decryptFileInfo(FileStream src, Rijndael alg)
        {
            string tmp = Path.GetTempFileName();
            byte[] bytes = new byte[src.readInt()];
            src.Read(bytes, 0, bytes.Length);
            File.WriteAllBytes(tmp, bytes);
            try
            {
                using (FileStream file = new FileStream(tmp, FileMode.Open))
                using (CryptoStream stream = new CryptoStream(file, alg.CreateDecryptor(), CryptoStreamMode.Read))
                {
                    BinaryFormatter f = new BinaryFormatter();
                    return f.Deserialize(stream) as FileInfo;
                }
            }
            catch
            {
                LastError = "The password used to decrypt the file is wrong";
                throw;
            }
            finally
            {
                File.Delete(tmp);
            }
        }
    }
}
