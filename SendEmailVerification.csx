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
    // Put your SendGrid bearer token value
    string bearer = "";
    try {
        // Get the email and correlationId from the query string
        string email = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "email", true) == 0).Value;
        string cid = req.GetQueryNameValuePairs().FirstOrDefault(q => string.Compare(q.Key, "correlationId", true) == 0).Value;

        // Generate a random 6 digit code
        Random random = new Random();
        const string chars = "1234567890";
        string passcode = new string(Enumerable.Repeat(chars, 6).Select(s => s[random.Next(s.Length)]).ToArray());

        // Send a request to SendGrid API to send out a email verification email using the generated random code
        HttpClient httpClient = new HttpClient();
        httpClient.BaseAddress = new Uri("https://api.sendgrid.com/v3/mail/send");
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearer);
        HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, httpClient.BaseAddress);
        string requestString = String.Format(
            @"{{""from"":{{""email"":""example@example.com""}},""personalizations"":[{{""to"":[{{""email"":""{0}""}}],""dynamic_template_data"":{{""code"":""{1}""}}}}],""template_id"":""<your_email_templated_id>""}}",
            email, passcode);
        request.Content = new StringContent(requestString, Encoding.UTF8, "application/json");
        await httpClient.SendAsync(request);

        // Generate a token for validation in the future
        string token = Guid.NewGuid().ToString();

        // Store the token, email, code, etc in Azure table storage
        var tableClient = CloudStorageAccount.Parse(connectionString).CreateCloudTableClient();
        var table = tableClient.GetTableReference("OtpStorage");
        OtpEntity otp = new OtpEntity(token);
        otp.Email = email;
        otp.CorrelationId = cid;
        otp.Code = passcode;
        otp.Created = DateTime.Now;
        otp.Status = "Sent";
        TableOperation insertOperation = TableOperation.Insert(otp);
        table.Execute(insertOperation);
        return req.CreateResponse(HttpStatusCode.OK, Encrypt(token, Encoding.UTF8.GetBytes(secret)));
    }
    catch (Exception ex) {
        return req.CreateResponse(HttpStatusCode.OK, ex.ToString());
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