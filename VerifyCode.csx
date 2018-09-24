#r "Microsoft.WindowsAzure.Storage"
using System;
using System.IO;
using System.Security.Cryptography;
using System.Net;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;

public static async Task<HttpResponseMessage> Run(HttpRequestMessage req, TraceWriter log)
{
    // Put your Azure Table Storage connections tring
    string connectionString = "";
    // Put any 16 byte secret string for encryption/decryption
    string secret = "";
    try {
        string email = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "email", true) == 0).Value;
        string cid = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "correlationId", true) == 0).Value;
        string token = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "token", true) == 0).Value;
        string passcode = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "code", true) == 0).Value;
        token = Decrypt(token, Encoding.UTF8.GetBytes(secret));

        var tableClient = CloudStorageAccount.Parse(connectionString).CreateCloudTableClient();
        var table = tableClient.GetTableReference("OtpStorage");
        TableOperation retrieveOperation = TableOperation.Retrieve<OtpEntity>(token.Substring(0, 2), token);
        TableResult result = table.Execute(retrieveOperation);
        if (result == null) {
            return req.CreateResponse(HttpStatusCode.BadRequest, "Cannot locate the entry");
        }

        OtpEntity otp = (OtpEntity)result.Result;
        if (otp.Code == passcode && otp.CorrelationId == cid) {
            if (DateTime.Now.AddMinutes(-60) > otp.Created) {
                return req.CreateResponse(HttpStatusCode.BadRequest, "Code has expired");
            }
            else {
                otp.Status = "Verified";
                TableOperation replaceOperation = TableOperation.Replace(otp);
                table.Execute(replaceOperation);
                return req.CreateResponse(HttpStatusCode.OK, Encrypt(token, Encoding.UTF8.GetBytes(secret)));
            }
        }
        else {
            return req.CreateResponse(HttpStatusCode.BadRequest, $"Code is invalid");
        }
    }
    catch (Exception ex) {
        return req.CreateResponse(HttpStatusCode.BadRequest, ex.ToString());
    }
}

public class OtpEntity : TableEntity
{
    public OtpEntity(string token)
    {
        this.PartitionKey = token.Substring(0, 2);
        this.RowKey = token;
        this.Token = token;
    }

    public OtpEntity() { }

    public string Email { get; set; }

    public string Code { get; set; }

    public string CorrelationId { get; set; }

    public DateTime Created { get; set; }

    public string Status { get; set; }

    public string Token { get; set; }
}

static string Encrypt(string plainText, byte[] key)
{       
    var toEncryptBytes = Encoding.UTF8.GetBytes(plainText);
    using (var provider = new AesCryptoServiceProvider())
    {
        provider.Key = key;
        provider.Mode = CipherMode.CBC;
        provider.Padding = PaddingMode.PKCS7;
        using (var encryptor = provider.CreateEncryptor(provider.Key, provider.IV))
        {
            using (var ms = new MemoryStream())
            {
                ms.Write(provider.IV, 0, 16);
                using (var cs = new CryptoStream(ms, encryptor, CryptoStreamMode.Write))
                {
                    cs.Write(toEncryptBytes, 0, toEncryptBytes.Length);
                    cs.FlushFinalBlock();
                }
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }
}

static string Decrypt(string cipherText, byte[] key)
{
    byte[] encryptedString = Convert.FromBase64String(cipherText);
    using (var provider = new AesCryptoServiceProvider())
    {
        provider.Key = key;
        provider.Mode = CipherMode.CBC;
        provider.Padding = PaddingMode.PKCS7;
        using (var ms = new MemoryStream(encryptedString))
        {
            byte[] buffer = new byte[16];
            ms.Read(buffer, 0, 16);
            provider.IV = buffer;
            using (var decryptor = provider.CreateDecryptor(provider.Key, provider.IV))
            {
                using (var cs = new CryptoStream(ms, decryptor, CryptoStreamMode.Read))
                {
                    byte[] decrypted = new byte[encryptedString.Length];
                    var byteCount = cs.Read(decrypted, 0, encryptedString.Length);
                    return Encoding.UTF8.GetString(decrypted, 0, byteCount);
                }
            }
        }
    }
}