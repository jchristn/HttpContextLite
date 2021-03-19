using System;
using HttpContextLite;

namespace Test
{
    class Program
    {
        static HttpServer _Server = new HttpServer(8000);

        static void Main(string[] args)
        {
            _Server.Logger = Console.WriteLine;
            _Server.Start();

            while (true)
            {

            }
        }
    }
}
