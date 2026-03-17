// using System.Diagnostics.CodeAnalysis;
// using System.Net;
// using ServiceStack.Text;
// using Xunit.Abstractions;
//
// namespace NLightning.Integration.Tests.BOLT10;
//
// using Infrastructure.Protocol.Services;
//
// /// <summary>
// /// These tests do not run on GitHub CI, For some reason DNS will not resolve
// /// </summary>
// public class DnsBootstrapTests
// {
//
//     public DnsBootstrapTests(ITestOutputHelper output)
//     {
//         // Console.SetOut(new TestOutputWriter(output));
//     }
//
//     [Theory]
//     [InlineData(true)]
//     [InlineData(false)]
//     public void Get_Bootstrap_Nodes_From_DNS_Seeds_IPv4(bool tcpOnly)
//     {
//         if (!tcpOnly && Environment.GetEnvironmentVariable("CI") == "true")
//         {
//             return; // Skip this test on GitHub Actions if using UDP
//         }
//
//         var results = DnsSeedClient.FindNodes(10, ["_lightning._dnstest.nlightn.ing"], false, useTcp: tcpOnly, [IPAddress.Parse("8.8.8.8")]);
//         Assert.True(results.Count > 0, "No seeds returned.");
//         foreach (var r in results)
//         {
//             $"{Convert.ToHexString(r.Pubkey).ToLower()}@{r.Endpoint.ToString().Replace(" ", string.Empty)}".Print();
//         }
//     }
//
//     [Theory, ExcludeFromCodeCoverage]
//     [InlineData(true)]
//     [InlineData(false)]
//     public void Get_Bootstrap_Nodes_From_DNS_Seeds_IPv6(bool tcpOnly)
//     {
//         if (!tcpOnly && Environment.GetEnvironmentVariable("CI") == "true")
//         {
//             return; // Skip this test on GitHub Actions if using UDP
//         }
//
//         var results = DnsSeedClient.FindNodes(10, ["dnstest.nlightn.ing"], true, useTcp: tcpOnly, [IPAddress.Parse("8.8.8.8")]);
//         Assert.True(results.Count > 0, "No seeds returned.");
//         foreach (var r in results)
//         {
//             $"{Convert.ToHexString(r.Pubkey).ToLower()}@{r.Endpoint.ToString().Replace(" ", string.Empty)}".Print();
//         }
//     }
// }