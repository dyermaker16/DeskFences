using System.IO;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows.Threading;
using DeskFences.Native;

namespace DeskFences.Services;

// Samples CPU, memory and network once a second (drives every 30 s) for the widgets, and keeps a
// minute of history so a new widget's graph is full straight away. Runs only while a widget is open.
public sealed class SystemMonitor
{
    public const int HistoryLength = 60;
    private const int DriveEveryTicks = 30;

    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _users;
    private int _ticksToDrives;

    // CPU and network are rates: the first sample after starting only sets these.
    private bool _primed;
    private ulong _lastIdle;
    private ulong _lastTotal;
    private long _lastIn;
    private long _lastOut;
    private DateTime _lastNetTime;
    private NetworkInterface[] _interfaces = Array.Empty<NetworkInterface>();
    private volatile bool _interfacesChanged = true;

    public History Cpu { get; } = new();        // percent
    public History Memory { get; } = new();     // percent
    public History NetIn { get; } = new();      // bytes per second
    public History NetOut { get; } = new();     // bytes per second

    public ulong MemoryUsed { get; private set; }
    public ulong MemoryTotal { get; private set; }
    public ulong CommitUsed { get; private set; }
    public ulong CommitTotal { get; private set; }
    public IReadOnlyList<DriveSample> Drives { get; private set; } = Array.Empty<DriveSample>();

    public event Action? Updated;

    public SystemMonitor()
    {
        _timer.Tick += (_, _) => Sample();
        // Raised on a worker thread: just flag it, the next sample re-reads the list.
        NetworkChange.NetworkAddressChanged += (_, _) => _interfacesChanged = true;
    }

    // Each widget calls AddUser when it opens and RemoveUser when it closes.
    public void AddUser()
    {
        if (_users++ > 0)
            return;
        _primed = false;
        _ticksToDrives = 0;
        Sample();
        _timer.Start();
    }

    public void RemoveUser()
    {
        if (--_users == 0)
            _timer.Stop();
    }

    private void Sample()
    {
        SampleCpu();
        SampleMemory();
        SampleNetwork();
        if (_ticksToDrives-- <= 0)
        {
            _ticksToDrives = DriveEveryTicks;
            SampleDrives();
        }
        _primed = true;
        Updated?.Invoke();
    }

    // Busy share of the time since the last sample, like Task Manager's "% Processor Time".
    private void SampleCpu()
    {
        if (!Win32.GetSystemTimes(out var idle, out var kernel, out var user))
            return;

        var total = kernel + user;
        if (_primed && total > _lastTotal)
        {
            var busy = 1.0 - (double)(idle - _lastIdle) / (total - _lastTotal);
            Cpu.Add(Math.Clamp(busy * 100, 0, 100));
        }
        _lastIdle = idle;
        _lastTotal = total;
    }

    private void SampleMemory()
    {
        var status = new Win32.MEMORYSTATUSEX { dwLength = (uint)Marshal.SizeOf<Win32.MEMORYSTATUSEX>() };
        if (!Win32.GlobalMemoryStatusEx(ref status) || status.ullTotalPhys == 0)
            return;

        MemoryTotal = status.ullTotalPhys;
        MemoryUsed = status.ullTotalPhys - status.ullAvailPhys;
        CommitTotal = status.ullTotalPageFile;
        CommitUsed = status.ullTotalPageFile - status.ullAvailPageFile;
        Memory.Add(100.0 * MemoryUsed / MemoryTotal);
    }

    // All connected adapters added up, like Rainmeter's NetIn/NetOut with no interface set.
    private void SampleNetwork()
    {
        var skip = !_primed;
        if (_interfacesChanged)
        {
            _interfacesChanged = false;
            _interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up
                            && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                            && n.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .ToArray();
            skip = true;   // the totals jump when the adapter list changes
        }

        long received = 0, sent = 0;
        foreach (var adapter in _interfaces)
        {
            try
            {
                var stats = adapter.GetIPStatistics();
                received += stats.BytesReceived;
                sent += stats.BytesSent;
            }
            catch (NetworkInformationException)
            {
                _interfacesChanged = true;   // adapter went away
            }
        }

        var now = DateTime.UtcNow;
        var seconds = (now - _lastNetTime).TotalSeconds;
        if (!skip && seconds > 0)
        {
            NetIn.Add(Math.Max(0, (received - _lastIn) / seconds));
            NetOut.Add(Math.Max(0, (sent - _lastOut) / seconds));
        }
        _lastIn = received;
        _lastOut = sent;
        _lastNetTime = now;
    }

    private void SampleDrives()
    {
        var drives = new List<DriveSample>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (drive.DriveType != DriveType.Fixed || !drive.IsReady || drive.TotalSize == 0)
                    continue;
                var name = drive.Name.TrimEnd(Path.DirectorySeparatorChar);
                drives.Add(new DriveSample(name, drive.TotalSize - drive.TotalFreeSpace, drive.TotalSize));
            }
            catch (IOException)
            {
                // Drive disappeared between listing and reading.
            }
        }
        Drives = drives;
    }
}

public sealed record DriveSample(string Name, long Used, long Total);

// The last HistoryLength values, oldest first.
public sealed class History
{
    private readonly double[] _values = new double[SystemMonitor.HistoryLength];
    private int _next;

    public int Count { get; private set; }

    public double this[int index] => _values[(_next - Count + index + _values.Length) % _values.Length];

    public double Latest => Count == 0 ? 0 : this[Count - 1];

    public void Add(double value)
    {
        _values[_next] = value;
        _next = (_next + 1) % _values.Length;
        if (Count < _values.Length)
            Count++;
    }

    public double Max()
    {
        var max = 0.0;
        for (var i = 0; i < Count; i++)
            max = Math.Max(max, this[i]);
        return max;
    }
}
