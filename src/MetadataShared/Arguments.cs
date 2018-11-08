﻿using DG.Tools;
using Microsoft.Crm.Sdk.Messages;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Messages;
using Microsoft.Xrm.Sdk.Metadata;
using Microsoft.Xrm.Sdk.Query;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Reflection;
using System.ServiceModel;
using System.Text.RegularExpressions;

namespace DG.Tools.XrmMockup.Metadata
{
    internal static class Arguments {

        public static ArgumentDescription Url = new ArgumentDescription() {
            Name = "url",
            Abbreviations = new string[] { }
        };

        public static ArgumentDescription ClientId = new ArgumentDescription()
        {
            Name = "clientid",
            Abbreviations = new string[] { }
        };

        public static ArgumentDescription RedirectUrl = new ArgumentDescription() {
            Name = "redirecturl",
            Abbreviations = new string[] { "rurl" }
        };

        public static ArgumentDescription OutDir = new ArgumentDescription() {
            Name = "outDir",
            Abbreviations = new string[] { "out", "o" }
        };

        public static ArgumentDescription Entities = new ArgumentDescription() {
            Name = "entities",
            Abbreviations = new string[] { "es" }
        };

        public static ArgumentDescription Solutions = new ArgumentDescription() {
            Name = "solutions",
            Abbreviations = new string[] { "ss" }
        };

        public static ArgumentDescription UnitTestProjectPath = new ArgumentDescription() {
            Name = "projectPath",
            Abbreviations = new string[] { "pp" }
        };

        public static ArgumentDescription fetchFromAssemblies = new ArgumentDescription()
        {
            Name = "fetchFromAssemblies",
            Abbreviations = new string[] { "fa" }
        };
        
        public static ArgumentDescription[] ArgList = new ArgumentDescription[] {
            Url,
            RedirectUrl,
            ClientId,
            Entities,
            Solutions,
            OutDir,
            UnitTestProjectPath,
            fetchFromAssemblies
        };
    }
}
