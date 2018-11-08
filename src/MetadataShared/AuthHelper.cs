using Chorus.LinqToXrm;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Client;
using System;
using System.Configuration;
using System.Net;


namespace DG.Tools.XrmMockup.Metadata
{
    internal class AuthHelper {
        private string redirecturl = ConfigurationManager.AppSettings["redirecturl"];
        private string clientid = ConfigurationManager.AppSettings["clientid"];
        private string url = ConfigurationManager.AppSettings["url"];

        public AuthHelper(
            string url, 
            string redirecturl, 
            string clientid) 
        {
            this.url = url;
            this.redirecturl = redirecturl;
            this.clientid = clientid;
        }

        internal IOrganizationService Authenticate() {
            return new XrmOrganizationService(this.url, this.clientid, redirectUrl: this.redirecturl);
        }

    }
}
