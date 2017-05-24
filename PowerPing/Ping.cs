﻿using System;
using System.Text;
using System.Threading.Tasks;
using System.Net;
using System.Net.Sockets;
using System.Net.NetworkInformation;
using System.Threading;
using System.Diagnostics;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;

/// <summary>
/// For constructing and sending ping ping packets
/// </summary>

namespace PowerPing
{

    class Ping
    {
        // Properties
        public PingResults Results { get; private set; } = new PingResults(); // Store current ping results
        public PingAttributes Attributes { get; private set; } = new PingAttributes(); // Stores the current operation's attributes
        public bool ShowOutput { get; set; } = true;
        public bool IsRunning { get; private set; } = false;
        public int Threads { get; set; } = 5;

        // Local variables
        private ConcurrentStack<IPAddress> activeHosts = new ConcurrentStack<IPAddress>(); // Stores found hosts during Scan()
        private bool cancelFlag = false;

        // Constructor
        public Ping() { }

        /// <summary>
        /// Sends a set of ping packets
        /// </summary>
        public void Send(PingAttributes attrs)
        {
            // Get inputted address
            string inputAddress = attrs.Address; 

            // Load user inputted attributes
            this.Attributes = attrs;

            // Lookup address
            Attributes.Address = PowerPing.Helper.VerifyAddress(Attributes.Address, Attributes.ForceV4 ? AddressFamily.InterNetwork : AddressFamily.InterNetworkV6);

            // Display intro message
            if (ShowOutput)
                PowerPing.Display.PingIntroMsg(inputAddress, this);

            // Perform ping operation
            this.SendICMP(Attributes);

            // Display stats
            if (ShowOutput)
                PowerPing.Display.PingResults(this);

        }
        /// <summary>
        /// Listen for an ICMPv4 packets
        /// </summary>
        public void Listen()
        {
            IPAddress localAddress = null;
            Socket listeningSocket = null;

            // Find local address
            foreach (var ip in Dns.GetHostEntry(Dns.GetHostName()).AddressList)
                if (ip.AddressFamily == AddressFamily.InterNetwork)
                    localAddress = ip;

            try
            {
                // Create listener socket
                listeningSocket = CreateRawSocket(AddressFamily.InterNetwork);
                // Bind socket to local address
                listeningSocket.Bind(new IPEndPoint(localAddress, 0));
                // Set SIO_RCVALL flag to socket IO control
                listeningSocket.IOControl(IOControlCode.ReceiveAll, new byte[] { 1, 0, 0, 0 }, new byte[] { 1, 0, 0, 0 });

                // Display initial message
                PowerPing.Display.ListenIntroMsg();

                // Listening loop
                while (true)
                {
                    byte[] buffer = new byte[4096];
                    // Endpoint for storing source address
                    EndPoint remoteEndPoint = new IPEndPoint(IPAddress.Any, 0);
                    // Recieve any incoming ICMPv4 packets
                    int bytesRead = listeningSocket.ReceiveFrom(buffer, ref remoteEndPoint);
                    // Create ICMP object of response
                    ICMP response = new ICMP(buffer, bytesRead);

                    // Display captured packet
                    PowerPing.Display.CapturedPacket(response, remoteEndPoint.ToString(), DateTime.Now.ToString("h:mm:ss.ff tt"), bytesRead);
                }
            }
            catch (SocketException)
            {
                PowerPing.Display.Error("Socket Error - Error occured while reading from socket\nPlease try again.", true);
            }
            catch (NullReferenceException)
            {
                PowerPing.Display.Error("Error fetching local address, connect to a network and try again.", true);
            }
        }
        /// <summary>
        /// ICMP Traceroute
        /// </summary>
        public void Trace() { }
        /// <summary>
        /// Recursive network scan method 
        /// </summary>
        /// <param name="range"></param>
        public void Scan(string range, bool recursing = false)
        {
            List<IPAddress> scanList = new List<IPAddress>();
            String[] ipSegments = range.Split('.');

            if (!recursing)
            {
                // Setup scan

                // Check format of address (for '-'s and disallow multipl '-'s in one segment, also check format of address


                // Holds the ranges for each ip segment
                int[] segLower = new int[4];
                int[] segUpper = new int[4];

                // Work out upper and lower ranges for each segment
                for (int y = 0; y < 4; y++)
                {
                    string[] ranges = ipSegments[y].Split('-');
                    segLower[y] = Convert.ToInt16(ranges[0]);
                    segUpper[y] = (ranges.Length == 1) ? segLower[y] : Convert.ToInt16(ranges[1]);
                }

                // Build list of addresses from ranges
                for (int seg1 = segLower[0]; seg1 <= segUpper[0]; seg1++)
                {
                    for (int seg2 = segLower[1]; seg2 <= segUpper[1]; seg2++)
                    {
                        for (int seg3 = segLower[2]; seg3 <= segUpper[2]; seg3++)
                        {
                            for (int seg4 = segLower[3]; seg4 <= segUpper[3]; seg4++)
                            {
                                scanList.Add(new IPAddress(new byte[] { (byte)seg1, (byte)seg2, (byte)seg3, (byte)seg4 }));
                            }
                        }
                    }
                }

                // Divide scanlist into lists for each thread
                List<IPAddress>[] threadLists = new List<IPAddress>[Threads];
                int splitListSize = (int)Math.Ceiling(scanList.Count / (double)Threads);
                int x = 0;

                for (int i = 0; i < threadLists.Length; i++)
                {
                    threadLists[i] = new List<IPAddress>();
                    for (int j = x; j < x + splitListSize; j++)
                    {
                        if (j >= scanList.Count)
                            break; // Stop if we are out of bounds
                        threadLists[i].Add(scanList[j]);
                    }
                    x += splitListSize;
                }

                // *Bug here*
                // Finally, fire up the threads!
                for (int i = 0; i < Threads - 1; i++)
                {
                    //Thread thread = new Thread(() => ScanThread(threadLists[i]));
                    //thread.Start();
                }

                // Display results of scan
                PowerPing.Display.ScanResult(scanList.Count, activeHosts.Count);
            }
            else
            {
                // Recursive ping sender

                // Setup ping attributes
                PingAttributes attrs = new PingAttributes();

                // Start ping operation

            }
        }
        /// <summary>
        /// ICMP flood
        /// </summary>
        public void Flood()
        {
            // Setup ping attributes
            PingAttributes attrs = new PingAttributes();
            attrs.Interval = 0;
            attrs.Timeout = 100;
            attrs.Continous = true;

            // Start threads
            for (int i = 0; i < Threads - 1; i++)
            {
                Thread thread = new Thread(() => SendICMP(attrs));
                //thread.Start();
            }

            // Start operation

            // Add loop here to listen for cancel flag
        }
        /// <summary>
        /// Stop any ping operations
        /// </summary>
        public void Stop()
        {
            // If a ping operation is running send cancel flag
            if (IsRunning)
            {
                cancelFlag = true;

                // wait till ping stops running
                while (IsRunning)
                    Task.Delay(25);
            }

            // Reset cancel flag
            cancelFlag = false;
        }

        private Socket CreateRawSocket(AddressFamily family)
        {
            Socket s = null;
            try
            {
                s = new Socket(family, SocketType.Raw, family == AddressFamily.InterNetwork ? ProtocolType.Icmp : ProtocolType.IcmpV6);
            }
            catch (SocketException)
            {
                PowerPing.Display.Error("Socket cannot be created\nPlease run as Administrator and try again.", true);
            }
            return s;
        }
        private void SendICMP(PingAttributes attrs)
        {
            IPEndPoint iep = null;
            EndPoint ep = null;
            IPAddress ipAddr = null;
            ICMP packet = new ICMP();
            Socket sock = null;
            Stopwatch responseTimer = new Stopwatch();
            int bytesRead, packetSize, index = 1;

            // Convert to IPAddress
            ipAddr = IPAddress.Parse(Attributes.Address);

            // Setup endpoint
            iep = new IPEndPoint(ipAddr, 0);
            ep = (EndPoint)iep;

            // Setup raw socket 
            sock = CreateRawSocket(ipAddr.AddressFamily);

            // Set socket options
            sock.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReceiveTimeout, Attributes.Timeout); // Socket timeout
            sock.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, Attributes.Ttl);
            //sock.Ttl = (short)attributes.ttl;

            // Construct our ICMP packet
            packet.type = Attributes.Type;
            packet.code = Attributes.Code;
            Buffer.BlockCopy(BitConverter.GetBytes(1), 0, packet.message, 0, 2); // Add seq num to ICMP message
            byte[] payload = Encoding.ASCII.GetBytes(Attributes.Message);
            Buffer.BlockCopy(payload, 0, packet.message, 4, payload.Length); // Add text into ICMP message
            packet.messageSize = payload.Length + 4;
            packetSize = packet.messageSize + 4;

            responseTimer.Start();

            // Sending loop
            while (Attributes.Continous ? true : index <= Attributes.Count)
            {
                // Exit loop if cancel flag recieved
                if (cancelFlag)
                    break;
                else
                    IsRunning = true;

                // Update ICMP checksum and seq
                packet.checksum = 0;
                Buffer.BlockCopy(BitConverter.GetBytes(index), 0, packet.message, 2, 2); // Include sequence number in ping message
                UInt16 chksm = packet.GetChecksum();
                packet.checksum = chksm;

                try
                {
                    // Send ping request
                    /// Try restart here?
                    sock.SendTo(packet.GetBytes(), packetSize, SocketFlags.None, iep); // Packet size = message field + 4 header bytes
                    Results.Sent++;

                    // Wait for response
                    byte[] buffer = new byte[1024];
                    bytesRead = sock.ReceiveFrom(buffer, ref ep);
                    responseTimer.Stop();

                    // Store reply packet
                    ICMP response = new ICMP(buffer, bytesRead);

                    // Display reply packet
                    if (ShowOutput)
                        PowerPing.Display.ReplyPacket(response, ep.ToString(), index, responseTimer.ElapsedMilliseconds);

                    // Store response time
                    Results.SetCurResponseTime(responseTimer.ElapsedMilliseconds);
                    Results.Recieved++;

                }
                catch (IOException)
                {
                    if (ShowOutput)
                        PowerPing.Display.Error("General transmit error");
                    Results.SetCurResponseTime(-1);
                    Results.Lost++;
                }
                catch (SocketException)
                {
                    if (ShowOutput)
                        PowerPing.Display.PingTimeout();
                    Results.SetCurResponseTime(-1);
                    Results.Lost++;
                }
                catch (Exception)
                {
                    if (ShowOutput)
                        PowerPing.Display.Error("General error occured");
                    Results.SetCurResponseTime(-1);
                    Results.Lost++;
                }
                finally
                {
                    // Increment seq and wait for interval
                    index++;
                    Thread.Sleep(Attributes.Interval);

                    responseTimer.Restart();
                }  
            }

            // Clean up
            IsRunning = false;
            sock.Close();
        }
    }

}