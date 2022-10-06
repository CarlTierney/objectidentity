using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Vision.ObjectIdentity.Tests
{
    [TestClass]
    public class UnitTest1
    {
        [TestMethod]
        public void TestInitializtion()
        {

            var connectionString = "server=.;database=vault;integrated security=true;TrustServerCertificate=Yes";

            var initializer = new SqlIdentityScopeInitializer(connectionString, "dbo", 20, false);

            var factory = new IdentityScopeFactory(initializer, 100);

            var manager = new IdentityManager(factory);

            var id = manager.GetNextIdentity<MerchantAccount, long>();

            Assert.IsTrue(id > 0);

        }


        [TestMethod]
        public void TestGettingNewBlocksSingleThread()
        {

            var connectionString = "server=.;database=vault;integrated security=true;TrustServerCertificate=Yes";

            var initializer = new SqlIdentityScopeInitializer(connectionString, "dbo", 20, false);

            var factory = new IdentityScopeFactory(initializer, 10);

            var manager = new IdentityManager(factory);

            

            var idsReceived = new List<long>();

            for (var i = 0;   i < 100; i++)
            { 
                var id = manager.GetNextIdentity<MerchantAccount, long>();
                idsReceived.Add(id);
            }

            Assert.IsTrue(idsReceived.Count == 100);

        }


        [TestMethod]
        public void TestGettingNewBlocksMultiThreaded()
        {

            var connectionString = "server=.;database=vault;integrated security=true;TrustServerCertificate=Yes";

            var initializer = new SqlIdentityScopeInitializer(connectionString, "dbo", 20, false);

            var factory = new IdentityScopeFactory(initializer, 10);

            var manager = new IdentityManager(factory);

            var tasks = new List<Task<List<long>>>();

            for (var i = 0; i < 10; i++)
            {
                var task = Task.Run<List<long>>(()=> GetIds(manager, 1000));
                tasks.Add(task);

            }

            Task.WaitAll(tasks.ToArray());

            var results = new List<long>();
            foreach (var t in tasks)
                results.AddRange(t.Result);

            var count = results.Count();

            var distinctCount = results.AsQueryable().Distinct().Count();

            Assert.IsTrue(count == distinctCount);
        }


        private List<long> GetIds(IdentityManager identityManager, int number)
        {
            var idsReceived = new List<long>();

            for (var i = 0; i < number; i++)
            {
                var id = identityManager.GetNextIdentity<MerchantAccount, long>();
                idsReceived.Add(id);
            }

            Assert.IsTrue(idsReceived.Count == number);

            return idsReceived;
        }


        private class MerchantAccount
        {
            public long Id { get; set; }
        }
    }
}
