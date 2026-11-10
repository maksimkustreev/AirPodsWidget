using System;
using System.Diagnostics;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Storage.Streams;

namespace AirPodsWidget
{
    /// <summary>Снимок состояния AirPods, полученный из BLE-рекламы Apple.</summary>
    public sealed class AirPodsStatus
    {
        public ushort ModelId { get; init; }
        public string ModelName { get; init; } = "AirPods";
        public bool IsMax => ModelId == 0x0A20;

        public int? Left { get; init; }      // 0..100 % или null (нет данных)
        public int? Right { get; init; }
        public int? Case { get; init; }

        public bool LeftCharging { get; init; }
        public bool RightCharging { get; init; }
        public bool CaseCharging { get; init; }

        public bool LeftInEar { get; init; }
        public bool RightInEar { get; init; }

        public short Rssi { get; init; }
        public DateTime Timestamp { get; init; }

        public bool HasAnyData => Left.HasValue || Right.HasValue || Case.HasValue;

        public bool SameValues(AirPodsStatus? o) =>
            o != null && o.ModelId == ModelId &&
            o.Left == Left && o.Right == Right && o.Case == Case &&
            o.LeftCharging == LeftCharging && o.RightCharging == RightCharging && o.CaseCharging == CaseCharging;
    }

    /// <summary>
    /// Слушает BLE-рекламу Apple (Proximity Pairing, тип 0x07) и извлекает из неё заряд
    /// левого/правого наушника и кейса. Точность — 10 % (так передают сами AirPods).
    /// </summary>
    public sealed class AirPodsBatteryMonitor : IDisposable
    {
        private const ushort AppleCompanyId = 0x004C;
        private const byte ProximityPairingType = 0x07;
        private const int PayloadLength = 27;

        /// <summary>Игнорировать слабый сигнал (чужие AirPods поблизости).</summary>
        public short MinRssi { get; set; } = -70;

        private BluetoothLEAdvertisementWatcher? _watcher;
        private AirPodsStatus? _lastSent;

        public event Action<AirPodsStatus>? StatusUpdated;

        public bool IsRunning => _watcher?.Status == BluetoothLEAdvertisementWatcherStatus.Started;

        public void Start()
        {
            try
            {
                if (_watcher == null ||
                    _watcher.Status == BluetoothLEAdvertisementWatcherStatus.Aborted)
                {
                    DisposeWatcher();
                    _watcher = new BluetoothLEAdvertisementWatcher
                    {
                        ScanningMode = BluetoothLEScanningMode.Active
                    };
                    _watcher.Received += OnReceived;
                    _watcher.Stopped += (_, e) => Debug.WriteLine($"BLE watcher stopped: {e.Error}");
                }

                if (_watcher.Status != BluetoothLEAdvertisementWatcherStatus.Started)
                    _watcher.Start();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BLE watcher start failed: {ex}");
            }
        }

        public void Stop()
        {
            try
            {
                if (_watcher != null && _watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started)
                    _watcher.Stop();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"BLE watcher stop failed: {ex}");
            }
        }

        private void OnReceived(BluetoothLEAdvertisementWatcher sender, BluetoothLEAdvertisementReceivedEventArgs args)
        {
            if (args.RawSignalStrengthInDBm < MinRssi) return;

            foreach (var md in args.Advertisement.ManufacturerData)
            {
                if (md.CompanyId != AppleCompanyId || md.Data == null) continue;
                if (md.Data.Length != PayloadLength) continue;

                var bytes = new byte[md.Data.Length];
                using (var reader = DataReader.FromBuffer(md.Data))
                    reader.ReadBytes(bytes);

                var status = Parse(bytes, args.RawSignalStrengthInDBm);
                if (status == null) continue;

                if (status.SameValues(_lastSent) &&
                    (status.Timestamp - _lastSent!.Timestamp).TotalSeconds < 20)
                    continue; // ничего не изменилось

                _lastSent = status;
                StatusUpdated?.Invoke(status);
            }
        }

        /// <summary>
        /// Формат (после Company ID 0x004C):
        /// [0]=0x07 тип, [1]=0x19 длина, [2] префикс, [3..4] модель,
        /// [5] статус (бит 0x20 — какой наушник «главный», младший полубайт — в ухе),
        /// [6] заряд наушников (два полубайта), [7] старший полубайт — флаги зарядки, младший — заряд кейса.
        /// Значения 0..10 → 0..100 %, 15 → нет данных.
        /// </summary>
        public static AirPodsStatus? Parse(byte[] d, short rssi)
        {
            if (d.Length != PayloadLength || d[0] != ProximityPairingType || d[1] != 0x19)
                return null;

            ushort model = (ushort)((d[3] << 8) | d[4]);
            byte status = d[5];
            bool flipped = (status & 0x20) == 0;

            int hiPods = (d[6] >> 4) & 0x0F;
            int loPods = d[6] & 0x0F;
            int leftRaw = flipped ? hiPods : loPods;
            int rightRaw = flipped ? loPods : hiPods;

            int chargeFlags = (d[7] >> 4) & 0x0F;
            int caseRaw = d[7] & 0x0F;
            // If the primary case nibble is unavailable (0xF), try the adjacent
            // manufacturer-data byte. Some firmware revisions emit case state there.
            if (caseRaw == 0x0F)
            {
                int altLo = d[8] & 0x0F;
                int altHi = (d[8] >> 4) & 0x0F;
                if (altLo <= 10) caseRaw = altLo;
                else if (altHi <= 10) caseRaw = altHi;
            }

            int inEar = status & 0x0F;

            return new AirPodsStatus
            {
                ModelId = model,
                ModelName = ModelName(model),
                Left = ToPercent(leftRaw),
                Right = ToPercent(rightRaw),
                Case = ToPercent(caseRaw),
                LeftCharging = (chargeFlags & (flipped ? 0b0010 : 0b0001)) != 0,
                RightCharging = (chargeFlags & (flipped ? 0b0001 : 0b0010)) != 0,
                CaseCharging = (chargeFlags & 0b0100) != 0,
                LeftInEar = (inEar & (flipped ? 0b1000 : 0b0010)) != 0,
                RightInEar = (inEar & (flipped ? 0b0010 : 0b1000)) != 0,
                Rssi = rssi,
                Timestamp = DateTime.Now
            };
        }

        private static int? ToPercent(int raw) => raw <= 10 ? raw * 10 : null;

        public static string ModelName(ushort id) => id switch
        {
            0x0220 => "AirPods",
            0x0F20 => "AirPods (2-го поколения)",
            0x1320 => "AirPods (3-го поколения)",
            0x0E20 => "AirPods Pro",
            0x1420 => "AirPods Pro (2-го поколения)",
            0x2420 => "AirPods Pro (2-го поколения, USB-C)",
            0x0A20 => "AirPods Max",
            _ => "AirPods"
        };

        private void DisposeWatcher()
        {
            if (_watcher == null) return;
            try
            {
                _watcher.Received -= OnReceived;
                if (_watcher.Status == BluetoothLEAdvertisementWatcherStatus.Started) _watcher.Stop();
            }
            catch { /* ignore */ }
            _watcher = null;
        }

        public void Dispose() => DisposeWatcher();
    }
}
