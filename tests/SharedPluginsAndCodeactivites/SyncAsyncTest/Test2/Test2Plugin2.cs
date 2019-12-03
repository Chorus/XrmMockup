﻿namespace DG.Some.Namespace.Test
{
    using System;
    using Microsoft.Xrm.Sdk;
    using DG.XrmFramework.BusinessDomain.ServiceContext;

    public class Test2Plugin2 : TestPlugin
    {
        public Test2Plugin2()
            : base(typeof(Test2Plugin2))
        {
            RegisterPluginStep<Account>(
                EventOperation.Update,
                ExecutionStage.PostOperation,
                ASync2NameUpdateSync)
                .AddFilteredAttributes(x => x.EMailAddress1)
                .SetExecutionMode(ExecutionMode.Asynchronous)
                .SetExecutionOrder(2);
        }

        protected void ASync2NameUpdateSync(LocalPluginContext localContext)
        {
            if (localContext == null)
            {
                throw new ArgumentNullException("localContext");
            }

            var service = localContext.OrganizationService;

            var account = Account.Retrieve(service, localContext.PluginExecutionContext.PrimaryEntityId, x => x.Name);

            var accountUpd = new Account(account.Id)
            {
                Name = account.Name + "ASync2"
            };
            service.Update(accountUpd);
        }
    }
}
