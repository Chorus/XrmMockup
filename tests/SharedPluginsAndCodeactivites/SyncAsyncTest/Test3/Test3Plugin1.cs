﻿namespace DG.Some.Namespace.Test
{
    using System;
    using Microsoft.Xrm.Sdk;
    using DG.XrmFramework.BusinessDomain.ServiceContext;

    public class Test3Plugin1 : TestPlugin
    {
        public Test3Plugin1()
            : base(typeof(Test3Plugin1))
        {
            RegisterPluginStep<Account>(
                EventOperation.Update,
                ExecutionStage.PostOperation,
                Sync1NameUpdate)
                .AddFilteredAttributes(x => x.EMailAddress1)
                .SetExecutionMode(ExecutionMode.Synchronous)
                .SetExecutionOrder(1);
        }

        protected void Sync1NameUpdate(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }

            var service = localContext.OrganizationService;

            var account = Account.Retrieve(service, localContext.PluginExecutionContext.PrimaryEntityId, x => x.Name);

            var accountUpd = new Account(account.Id)
            {
                Name = account.Name + "Sync1"
            };
            service.Update(accountUpd);
        }
    }
}
