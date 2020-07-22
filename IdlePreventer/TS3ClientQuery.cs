﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Net;
using System.Net.Sockets;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Runtime.Remoting;
using System.Text.RegularExpressions;

namespace IdlePreventer
{
	class TS3ClientQuery
	{
		private string serverIP;
		private int serverPort;
		private TcpClient TcpClient;
		private Socket socket;
		private NetworkStream stream;
		public int tsClientId;
		public Int32 tsChannelId;
		public string tsChannelPath;
		public string tsClientNickname;
		public bool tsPluginConnected = false;
		public bool tsServerConnected = false;
		public string tsServerIp;
		public int tsServerPort;
		private Form1 MW;

		public TS3ClientQuery(string ip, int port, Form1 window)
		{
			serverIP = ip;
			serverPort = port;
			MW = window;
		}
		public TS3ClientQuery(string ip, int port, Form1 window, string key)
		{
			serverIP = ip;
			serverPort = port;
			MW = window;
			ConnectAndAuthorizeAsync(key);
		}

		public async Task ConnectAndAuthorizeAsync(string APIkey)
		{
			MW.connectionStatusLabel.Text = "connecting...";
			try
			{
				if (TcpClient != null && TcpClient.Connected)
					TcpClient.Close();
				if (socket != null && socket.Connected)
					socket.Close();
				TcpClient = await Task.Run(() => { return new TcpClient(serverIP, serverPort); });
				socket = await Task.Run(() => { return new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp); });
				stream = TcpClient.GetStream();
				socket.Connect(serverIP, serverPort);
				tsPluginConnected = true;
			}
			catch (SocketException ex)
			{
				MW.connectionStatusLabel.Text = "not connected;\nCould not connect to TS3ClientQuery.";
				tsPluginConnected = false;
			}

			string response = await SendMessageAsync("auth apikey=" + APIkey);
			Debug.WriteLine("auth response =>" + response + "<=");
			response = response.Remove(0, response.IndexOf("See \"help auth\" for details.") + "See \"help auth\" for details.".Length);
			IDictionary<string,string> serResponse = SerializeResponse(response);
			if (serResponse["msg"] == "invalid\\sparameter")
				MW.connectionStatusLabel.Text = "not connected\ninvalid token!";
			else if (serResponse["msg"] == "ok")
			{
				try
				{
					Properties.Settings.Default["APIkey"] = MW.apiKeyTextBox.Text;
					Properties.Settings.Default.Save();
					await GetMyConnectionInfoAsync();
				}
				catch(Exception ex)
				{
					MessageBox.Show("Error: " + ex.ToString());
				}
			}
		}

		//public async Task<string> SendMessageAsync2(string message)
		//{
		//	try
		//	{
		//		Byte[] data = ASCIIEncoding.ASCII.GetBytes(message + "\n");
		//		await stream.WriteAsync(data, 0, data.Length);
		//		MW.connectionStatusLabel.Text = "Sent:\n" + message;

		//		data = new byte[1024];
		//		int bytesAmount = await stream.ReadAsync(data, 0, data.Length);
		//		string responseData = Encoding.ASCII.GetString(data, 0, bytesAmount);
		//		MW.connectionStatusLabel.Text = "Recieved:\n" + responseData;

		//		tsPluginConnected = true;
		//		return responseData;
		//	}
		//	catch (ArgumentNullException exc) { MW.connectionStatusLabel.Text = "ArgumentNull Exception: " + exc; return exc.ToString(); }
		//	catch (SocketException exc) { MW.connectionStatusLabel.Text = "Socket Exception: " + exc; return exc.ToString(); }
		//	catch (IOException exc)
		//	{

		//		Debug.WriteLine("socket exc to debug ->" + exc.Message + "<- socket exc to debug end");
		//		if (exc.Message == "Unable to read data from the transport connection: An established connection was aborted by the software in your host machine.")
		//		{
		//			MW.connectionStatusLabel.Text = "not connected;\nLost connection to TS3ClientQuery";
		//			tsPluginConnected = false;
		//			return exc.ToString();
		//		}
		//		else if (exc.Message == "Unable to write data to the transport connection: An established connection was aborted by the software in your host machine.")
		//		{
		//			MW.connectionStatusLabel.Text = "not connected;\nCould not connect to TS3ClientQuery";
		//			tsPluginConnected = false;
		//			return exc.ToString();
		//		}
		//		else
		//		{
		//			MW.connectionStatusLabel.Text = "Socket Exception:\n" + exc;
		//			return exc.ToString();
		//		}

		//	}
		//	catch (Exception exc) { MW.connectionStatusLabel.Text = "Exception:\n" + exc; return exc.ToString(); }
		//}

		public async Task<string> SendMessageAsync(string message)
		{
			try
			{
				Byte[] data = ASCIIEncoding.ASCII.GetBytes(message + "\n");
				await stream.WriteAsync(data, 0, data.Length);
				MW.connectionStatusLabel.Text = "Sent:\n" + message;


				data = new byte[1];
				int bytesAmount;
				string response;
				using (MemoryStream ms = new MemoryStream())
				{
					do
					{
						bytesAmount = await stream.ReadAsync(data, 0, data.Length);
						ms.Write(data, 0, bytesAmount);
					} while (stream.DataAvailable);

					response = Encoding.ASCII.GetString(ms.ToArray(), 0, (int)ms.Length);
				}
				MW.connectionStatusLabel.Text = "Recieved:\n" + response;

				tsPluginConnected = true;
				return response;
			}
			catch (ArgumentNullException exc) { MW.connectionStatusLabel.Text = "ArgumentNull Exception: " + exc; return exc.ToString(); }
			catch (SocketException exc) { MW.connectionStatusLabel.Text = "Socket Exception: " + exc; return exc.ToString(); }
			catch (IOException exc)
			{
				if (exc.Message == "Unable to read data from the transport connection: An established connection was aborted by the software in your host machine.")
				{
					MW.connectionStatusLabel.Text = "not connected;\nLost connection to TS3ClientQuery";
					tsPluginConnected = false;
					return "Exception " + exc.HResult;
				}
				else if (exc.Message == "Unable to write data to the transport connection: An established connection was aborted by the software in your host machine.")
				{
					MW.connectionStatusLabel.Text = "not connected;\nCould not connect to TS3ClientQuery";
					tsPluginConnected = false;
					return "Exception " + exc.HResult;
				}
				else
				{
					MW.connectionStatusLabel.Text = "Socket Exception:\n" + exc;
					return "Exception " + exc.HResult;
				}

			}
			catch (Exception exc) { MW.connectionStatusLabel.Text = "Exception:\n" + exc; return exc.Message; }
		}

		public IDictionary<string, string> SerializeResponse(string response)
		{
			string pattern = "(=)";
			string[] buffer = Regex.Split(response, pattern);

			int i = 0;
			string[] splitResponse = new string[buffer.Length * 3];
			foreach (string item in buffer)
			{
				char[] separator = new char[]
				{
					' ',
					'\n'
				};
				string[] splitItem = item.Split(separator);
				foreach (string s in splitItem)
				{
					splitResponse[i] = s;
					i++;
				}

			}

			IDictionary<string, string> result = new Dictionary<string, string>();
			for (i = 0; i < splitResponse.Length; i++)
			{
				if (splitResponse[i] == "=")
				{
					result[splitResponse[i - 1]] = splitResponse[i + 1];
				}
			}
			return result;
		}

		private bool getClientCalled = false;
		public async Task GetMyConnectionInfoAsync()
		{
			getClientCalled = true;
			try
			{
				string response = await SendMessageAsync("whoami");
				IDictionary<string, string> serResponse = SerializeResponse(response);
				tsChannelId = int.Parse(serResponse["cid"]);
				tsClientId = int.Parse(serResponse["clid"]);

				response = await SendMessageAsync("clientvariable clid=" + tsClientId + " client_nickname");
				tsClientNickname = SerializeResponse(response)["client_nickname"];

				response = await SendMessageAsync("channelconnectinfo");
				tsChannelPath = SerializeResponse(response)["path"];



				response = await SendMessageAsync("serverconnectinfo");
				serResponse = SerializeResponse(response);
				tsServerIp = serResponse["ip"];
				tsServerPort = int.Parse(serResponse["port"]);
				
				MW.connectionStatusLabel.Text = "connected";
				tsServerConnected = true;
			}
			catch (KeyNotFoundException ex)
			{
				Debug.WriteLine(ex);

				MW.connectionStatusLabel.Text = "connected\nYou are not connected to any teamspeak server.";
				tsServerConnected = false;
				await Task.Delay(5000);
				GetMyConnectionInfoAsync();
			}
			getClientCalled = false;
		}
	}
}
