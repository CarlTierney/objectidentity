using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ObjectIdentity;
using Microsoft.Extensions.Configuration;

namespace Vision.ObjectIdentity.Tests
{
    [TestClass]
    public class IdentityTests
    {
        private static string _dbConnString = "";
        
        [ClassInitialize]
        public static void Setup(TestContext context)
        {
            var cb = new ConfigurationBuilder();
            cb.AddUserSecrets<IdentityTests>()
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
            var config  = cb.Build();

           

            _dbConnString = config["testdb"] ?? config.GetConnectionString("testdb");
        }

        [TestMethod]
        public void TestInitialization()
        {
       

            
            
            var initializer = new SqlIdentityScopeInitializer(_dbConnString, "dbo", false);
            var factory = new IdentityScopeFactory(initializer);
            var manager = new IdentityManager(factory);

            var id = manager.GetNextIdentity<LedgerTransaction, long>();

            Assert.IsTrue(id > 0);
        }

        [TestMethod]
        public void TestGettingNewBlocksSingleThread()
        {
           
            
            var initializer = new SqlIdentityScopeInitializer(_dbConnString, "dbo", false);
            var factory = new IdentityScopeFactory(initializer);
            var manager = new IdentityManager(factory);

            var idsReceived = new List<long>();

            for (var i = 0; i < 100; i++)
            {
                var id = manager.GetNextIdentity<LedgerTransaction, long>();
                idsReceived.Add(id);
            }

            Assert.AreEqual(100, idsReceived.Count);
        }

        [TestMethod]
        public void TestGettingNewBlocksMultiThreaded()
        {
            var initializer = new SqlIdentityScopeInitializer(_dbConnString, "dbo",  false);
            var factory = new IdentityScopeFactory(initializer);
            var manager = new IdentityManager(factory);

            var tasks = new List<Task<List<long>>>();

            for (var i = 0; i < 10; i++)
            {
                var task = Task.Run(() => GetIds(manager, 1000));
                tasks.Add(task);
            }

            Task.WaitAll(tasks.ToArray());

            var results = new List<long>();
            foreach (var t in tasks)
                results.AddRange(t.Result);

            var count = results.Count;
            var distinctCount = results.Distinct().Count();

            Assert.AreEqual(count, distinctCount);
        }

        [TestMethod]
        public void TestInitializeScopeWithStartingId()
        {
            var initializer = new SqlIdentityScopeInitializer(_dbConnString, "dbo", false);
            var factory = new IdentityScopeFactory(initializer);
            var manager = new IdentityManager(factory);

            manager.IntializeScope<long>("TestScope", 1000);

            var id = manager.GetNextIdentity<TestScope, long>();

            Assert.IsTrue(id >= 1000);
        }

        [TestMethod]
        public void TestInitializeScopeWithTypeName()
        {
            var initializer = new SqlIdentityScopeInitializer(_dbConnString, "dbo",  false);
            var factory = new IdentityScopeFactory(initializer);
            var manager = new IdentityManager(factory);

            manager.InitializeScope<TestScope, long>(1000);

            var id = manager.GetNextIdentity<TestScope, long>();

            Assert.IsTrue(id >= 1000);
        }

        private List<long> GetIds(IdentityManager identityManager, int number)
        {
            var idsReceived = new List<long>();

            for (var i = 0; i < number; i++)
            {
                var id = identityManager.GetNextIdentity<LedgerTransaction, long>();
                idsReceived.Add(id);
            }

            Assert.AreEqual(number, idsReceived.Count);

            return idsReceived;
        }

        private class LedgerTransaction
        {
            public long Id { get; set; }
        }

        private class TestScope
        {
            public long Id { get; set; }
        }
    }
}
