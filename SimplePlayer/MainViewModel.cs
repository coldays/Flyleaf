using FlyleafLib.MediaPlayer;
using System.ComponentModel;

namespace SimplePlayer;

public class MainViewModel : INotifyPropertyChanged
{
    public MainViewModel()
    {
        Player = new Player(new FlyleafLib.Config
        {
            Demuxer = new FlyleafLib.Config.DemuxerConfig
            {
                // Reset flags since default is discard corrupt
                FormatFlags = 0,
            }
        });
    }

    private Player _player;
    public Player Player
    {
        get => _player;
        set
        {
            _player = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Player)));
        }
    }

    private string _text = "TEST TEST TEST TEST TEST";
    public string Text
    {
        get => _text;
        set
        {
            _text = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Text)));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
}
