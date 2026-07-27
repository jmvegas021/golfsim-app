using System.Net;
using System.Runtime.InteropServices;

namespace GsproLighting.Gspro.Discovery;

/// <summary>
/// Reads the Windows TCP connection table with owning PIDs (best-effort).
/// </summary>
public sealed class WindowsTcpConnectionReader
{
    private const int AfInet = 2;
    private const int TcpTableOwnerPidAll = 5;

    public IReadOnlyList<TcpRow> ReadIpv4Rows()
    {
        if (!OperatingSystem.IsWindows())
            return Array.Empty<TcpRow>();

        var size = 0;
        GetExtendedTcpTable(IntPtr.Zero, ref size, true, AfInet, TcpTableOwnerPidAll, 0);
        if (size <= 0)
            return Array.Empty<TcpRow>();

        var buffer = Marshal.AllocHGlobal(size);
        try
        {
            var result = GetExtendedTcpTable(buffer, ref size, true, AfInet, TcpTableOwnerPidAll, 0);
            if (result != 0)
                return Array.Empty<TcpRow>();

            var count = Marshal.ReadInt32(buffer);
            var rows = new List<TcpRow>(count);
            var rowPtr = IntPtr.Add(buffer, 4);
            var rowSize = Marshal.SizeOf<MibTcpRowOwnerPid>();

            for (var i = 0; i < count; i++)
            {
                var native = Marshal.PtrToStructure<MibTcpRowOwnerPid>(rowPtr);
                rows.Add(new TcpRow(
                    ToIp(native.LocalAddr),
                    ToPort(native.LocalPort),
                    ToIp(native.RemoteAddr),
                    ToPort(native.RemotePort),
                    native.OwningPid,
                    MapState(native.State)));
                rowPtr = IntPtr.Add(rowPtr, rowSize);
            }

            return rows;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    private static string ToIp(uint addr) =>
        new IPAddress(BitConverter.GetBytes(addr)).ToString();

    private static int ToPort(uint port) =>
        (int)(((port & 0xFF) << 8) | ((port >> 8) & 0xFF));

    private static string MapState(uint state) => state switch
    {
        1 => "Closed",
        2 => "Listen",
        3 => "SynSent",
        4 => "SynReceived",
        5 => "Established",
        6 => "FinWait1",
        7 => "FinWait2",
        8 => "CloseWait",
        9 => "Closing",
        10 => "LastAck",
        11 => "TimeWait",
        12 => "DeleteTcb",
        _ => $"State{state}"
    };

    [DllImport("iphlpapi.dll", SetLastError = true)]
    private static extern uint GetExtendedTcpTable(
        IntPtr pTcpTable,
        ref int dwOutBufLen,
        bool sort,
        int ipVersion,
        int tblClass,
        uint reserved);

    [StructLayout(LayoutKind.Sequential)]
    private struct MibTcpRowOwnerPid
    {
        public uint State;
        public uint LocalAddr;
        public uint LocalPort;
        public uint RemoteAddr;
        public uint RemotePort;
        public int OwningPid;
    }

    public readonly record struct TcpRow(
        string LocalAddress,
        int LocalPort,
        string RemoteAddress,
        int RemotePort,
        int ProcessId,
        string State);
}
