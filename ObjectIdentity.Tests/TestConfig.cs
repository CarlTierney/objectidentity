using Microsoft.Extensions.Configuration;
using System;
using System.IO;

namespace ObjectIdentity.Tests
{
    /// <summary>
    /// Provides configuration services for tests, including connection strings
    /// </summary>
    public static class TestConfig
    {
        private static readonly Lazy<IConfiguration> _configuration = new Lazy<IConfiguration>(() =>
        {
            return new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables()
                .Build();
        });

        /// <summary>
        /// Gets the test database connection string, using environment variables if available (for CI/CD)
        /// or falling back to appsettings.json for local development
        /// </summary>
        public static string GetTestDbConnectionString()
        {
            return _configuration.Value.GetConnectionString("testdb")
                ?? "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Database=SequentialIdTests;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True";
        }

        /// <summary>
        /// Gets a configuration instance that can be used in tests
        /// </summary>
        public static IConfiguration Configuration => _configuration.Value;
    }
}
