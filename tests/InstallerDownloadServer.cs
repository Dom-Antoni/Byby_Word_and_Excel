using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;

internal static class InstallerDownloadServer
{
    public static int Main(string[] args)
    {
        var server=new TcpListener(IPAddress.Loopback,0);server.Start();
        File.WriteAllText(args[1]+".tmp",((IPEndPoint)server.LocalEndpoint).Port.ToString());File.Move(args[1]+".tmp",args[1]);
        while(true)using(var client=server.AcceptTcpClient())using(var stream=client.GetStream())
        {
            string request;using(var reader=new StreamReader(stream,Encoding.ASCII,false,1024,true)){request=reader.ReadLine();while(!string.IsNullOrEmpty(reader.ReadLine())){}}
            var info=new FileInfo(args[0]);byte[] headers=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/zip\r\nContent-Length: "+info.Length+"\r\nConnection: close\r\n\r\n");
            try
            {
                stream.Write(headers,0,headers.Length);
                if(request!=null && !request.StartsWith("HEAD "))using(var file=File.OpenRead(args[0]))
                {
                    if(request.Contains("/bad.zip")){int first=file.ReadByte();stream.WriteByte((byte)(first^1));}file.CopyTo(stream);
                }
            }
            catch(IOException){} // A deliberately rejected or cancelled download may close early.
        }
    }
}
