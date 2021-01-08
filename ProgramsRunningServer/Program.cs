using Shared;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ProgramsRunningServer
{
    internal class Program
    {
        public static bool keepAwake;

        private static void Main()
        {
            keepAwake = false;

            var localPort = Validator.Validate<int>(ConfigurationManager.AppSettings["TargetPort"]);
            var localIpAddress = Validator.Validate<IPAddress>(ConfigurationManager.AppSettings["LocalIpAddress"]);
            var clientIpAddress = Validator.Validate<IPAddress>(ConfigurationManager.AppSettings["TargetIpAddress"]);
            var clientMacAddress = Validator.Validate<string>(ConfigurationManager.AppSettings["TargetMacAddress"]);
            var repeatPeriod = Validator.Validate<int>(ConfigurationManager.AppSettings["TimeoutInMinutes"]);

            var thread = new Thread(() => WakeOnLan(clientMacAddress, clientIpAddress));
            thread.Start();

            while (true)
            {
                var listener = new TcpListener(localIpAddress, localPort);
                listener.Start();

                Console.WriteLine("Listening...");
                var client = listener.AcceptTcpClient();

                try
                {
                    var networkStream = client.GetStream();
                    var bytesToSend = Encoding.ASCII.GetBytes(keepAwake.ToString());

                    Console.WriteLine("Sending : " + keepAwake.ToString());
                    networkStream.Write(bytesToSend, 0, bytesToSend.Length);

                    Thread.Sleep(5000);
                }
                catch (Exception ex)
                {
                    File.AppendAllText("errors.txt", ex.Message + Environment.NewLine);
                }

                client.Close();
                listener.Stop();
                Thread.Sleep(60000 * repeatPeriod);
            }
        }

        public static void WakeOnLan(string clientMacAddress, IPAddress clientIpAddress)
        {
            var programList = Validator.Validate<List<string>>(ConfigurationManager.AppSettings["ProgramList"]);

            while (true)
            {
                keepAwake = IsAnyOfProgramsRunning(programList);

                if (!keepAwake)
                {
                    Thread.Sleep(20000);
                    continue;
                }

                Console.WriteLine("Sending Wake On Lan packet");

                var payloadIndex = 0;
                var payload = new byte[1024];

                for (var i = 0; i < 6; i++)
                {
                    payload[payloadIndex] = 255;
                    payloadIndex++;
                }

                clientMacAddress = Regex.Replace(clientMacAddress, "[-|:]", "");

                for (var j = 0; j < 16; j++)
                {
                    for (var k = 0; k < clientMacAddress.Length; k += 2)
                    {
                        var s = clientMacAddress.Substring(k, 2);
                        payload[payloadIndex] = byte.Parse(s, NumberStyles.HexNumber);
                        payloadIndex++;
                    }
                }

                var sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp)
                {
                    EnableBroadcast = true
                };

                sock.SendTo(payload, new IPEndPoint(clientIpAddress, 0));
                sock.Close(5000);

                Thread.Sleep(20000);
            }
        }

        public static bool IsAnyOfProgramsRunning(List<string> programList)
        {
            var processes = Process.GetProcesses();

            foreach (var process in processes)
            {
                if (programList.Contains(process.ProcessName))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
