using System;

namespace SpotifyApp
{
    class Program
    {
        static void Main(string[] args)
        {
            bool isContinue = true;
            Player spotify = new Player();
            spotify.StartPlayer(spotify);
            Console.WriteLine("App is running! Enter 'play', 'pause', or 'q'");
            while (isContinue)
            {
                var input = Console.ReadLine().ToUpper();
                switch (input)
                {
                    case "Q":
                        Console.WriteLine("shutting down...");
                        isContinue = false;
                        spotify.StopPlayer();
                        break;
                    case "PLAY":
                        spotify.InvokeSpotify(Commands.PLAY);
                        break;
                    case "PAUSE":
                        spotify.InvokeSpotify(Commands.PAUSE);
                        break;
                    default: break;
                }
            }

        }
    }
}
