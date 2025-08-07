using FlyleafLib;
using FlyleafLib.Controls;
using FlyleafLib.MediaPlayer;
using System.Windows;
using System.Windows.Interop;

namespace FlyeleafPlayer__D3DImage_
{
    public class D3DPlayer : D3DRenderer, IHostPlayer, IDisposable
    {
        public D3DPlayer()
        {
            Player = new Player();
            Player.Host = this;
            Loaded += D3DPlayer_Loaded;
        }

        private void D3DPlayer_Loaded(object sender, RoutedEventArgs e)
        {
            var handle = new WindowInteropHelper(Application.Current.MainWindow).Handle;

            Player.VideoDecoder.CreateSwapChain(handle);
            SetBackBuffer(Player.renderer.BackBuffer);
            Player.Open(@"C:\Users\aka\OneDrive - Teleplan AS\MediaTest\Videos\kraken3.ts");
            Player.Play();
        }

        public Player Player { get; set; }

        public bool Player_CanHideCursor() => false;
        public void Player_Disposed()
        {

        }
        public bool Player_GetFullScreen()
        {
            return false;
        }
        public void Player_SetFullScreen(bool value)
        {

        }

        public void Dispose()
        {

        }
    }
}
