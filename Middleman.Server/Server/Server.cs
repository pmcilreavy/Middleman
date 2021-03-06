﻿using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using Middleman.Server.Configuration;
using Middleman.Server.Handlers;
using NLog;

namespace Middleman.Server.Server
{
    public class Server
    {
        private static readonly Logger Log = LogManager.GetCurrentClassLogger();

        #region Constructors

        public Server(ListenerConfiguration lc)
        {
            _lc = lc;
            Port = lc.ListenPort;
            UseHttps = lc.ListenSsl;
            CertSearchString = lc.SslCertName;
            DestinationWebRoot = lc.DestinationHost;
        }

        #endregion Constructors

        #region Fields

        private readonly ListenerConfiguration _lc;

        public int Port { get; private set; }

        #endregion Fields

        #region Properties

        public string CertSearchString { get; private set; }

        public string DestinationWebRoot { get; private set; }

        public bool UseHttps { get; private set; }

        #endregion Properties

        #region Methods

        public void Start()
        {
            var endPoint = new IPEndPoint(IPAddress.Parse(_lc.ListenIp), Port);
            var handler = new ReverseProxyHandler(DestinationWebRoot);

            handler.RewriteHost = true;
            handler.AddForwardedForHeader = false;
            handler.RemoveExpectHeader = false;

            if (UseHttps)
            {
                var cert = GetCertificate();

                //string pwd = "password";
                //var suppliers = new[] { "CN=localhost" };
                //var cb = new X509CertBuilder(suppliers, "CN=Middleman.Server.DO_NOT_TRUST", CertStrength.bits_512);
                //X509Certificate2 newcert = cb.MakeCertificate(pwd, "CN=localhost", 5);

                //cb.AddCertToStore(newcert, StoreName.Root, StoreLocation.LocalMachine);

                ////File.WriteAllBytes("cert.pfx", newcert.Export(X509ContentType.Pkcs12, pwd));
                ////File.WriteAllBytes("cert.cer", newcert.Export(X509ContentType.Cert, pwd));

                var server = new SecureMiddlemanServer(endPoint, handler, cert);

                server.Start();
            }
            else
            {
                var server = new MiddlemanServer(endPoint, handler);

                server.Start();
            }


            //Common.Log("UseHttps: " + UseHttps);
            //Common.Log("Post: " + Post);
            //Common.Log("CertSearchString: " + CertSearchString);
            //Common.Log("DestinationWebRoot: " + DestinationWebRoot);
            //Common.Log("Port: " + port);

            //if (!string.IsNullOrWhiteSpace(CertSearchString))
            //{
            //    _serverCertificate = GetCertificate();

            //    Common.Log("Server Certificate: " + (_serverCertificate != null ? _serverCertificate.Subject : "NOT FOUND!"));
            //}
        }

        private static bool UserCertificateValidationCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
        {
            return true;
        }


        //private X509Certificate ServerCertificateSelectionCallback(object sender, string targetHost, X509CertificateCollection localCertificates, X509Certificate remoteCertificate, string[] acceptableIssuers)
        //{
        //    return _serverCertificate;
        //}


        private X509Certificate2 FindStoreCert(X509Store sslStore)
        {
            X509Certificate2 returnCert = null;

            sslStore.Open(OpenFlags.ReadOnly);

            if (sslStore.Certificates.Count > 0)
            {
                foreach (var cert in sslStore.Certificates)
                {
                    //Log.Debug(sslStore.Name + " // " + cert.Subject);
                    if (cert.Subject.ToLowerInvariant().Contains(CertSearchString.ToLowerInvariant()))
                    {
                        returnCert = cert;
                    }
                }
            }
            return returnCert;
        }

        private X509Certificate2 GetCertificate()
        {
            X509Certificate2 returnCert = null;
            X509Store sslStore = null;

            try
            {
                sslStore = new X509Store("Root", StoreLocation.LocalMachine);
                returnCert = FindStoreCert(sslStore);

                if (returnCert == null)
                {
                    sslStore.Close();

                    sslStore = new X509Store("My", StoreLocation.CurrentUser);
                    returnCert = FindStoreCert(sslStore);
                }
            }
            catch
            {
                //
            }
            finally
            {
                if (sslStore != null)
                {
                    sslStore.Close();
                }
            }

            return returnCert;
        }

        #endregion Methods
    }
}