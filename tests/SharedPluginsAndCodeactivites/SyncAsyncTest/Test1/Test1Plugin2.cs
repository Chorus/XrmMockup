﻿namespace DG.Some.Namespace.Test
{
    using System;
    using Microsoft.Xrm.Sdk;
    using DG.XrmFramework.BusinessDomain.ServiceContext;

    public class Test1Plugin2 : TestPlugin
    {
        public Test1Plugin2()
            : base(typeof(Test1Plugin2))
        {
            RegisterPluginStep<Account>(
                EventOperation.Update,
                ExecutionStage.PostOperation,
                Sync2NameUpdate)
                .AddFilteredAttributes(x => x.EMailAddress1)
                .SetExecutionOrder(2);
        }

        protected void Sync2NameUpdate(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }

            var service = localContext.OrganizationService;

            var account = Account.Retrieve(service, localContext.PluginExecutionContext.PrimaryEntityId, x => x.Name);

            var accountUpd = new Account(account.Id)
            {
                Name = account.Name + "Sync2"
            };
            service.Update(accountUpd);
        }
    }
}
