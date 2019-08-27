using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

// Classes for managing IOF XML 3.0 Route element.
// IOF XML 3.0 specification: https://github.com/international-orienteering-federation/datastandard-v3.
// IOF XML 3.0 example result list file with Route element: https://github.com/international-orienteering-federation/datastandard-v3/blob/master/examples/ResultList1.xml.

/// <summary>
/// Class representing a route, including logic for converting to/from an IOF XML 3.0 route stored in binary format.
/// </summary>
public class IofXml30Route
{
  private double? length;
  private IEnumerable<IofXml30Waypoint> waypoints = new List<IofXml30Waypoint>();

  /// <summary>
  /// The waypoints of the route.
  /// </summary>
  public IEnumerable<IofXml30Waypoint> Waypoints
  {
    get { return waypoints; }
    set { waypoints = value ?? new List<IofXml30Waypoint>(); }
  }

  /// <summary>
  /// Writes the route in IOF XML 3.0 binary format to the specified stream.
  /// </summary>
  /// <param name="stream">The stream to write to.</param>
  public void WriteToStream(Stream stream)
  {
    IofXml30Waypoint previousWaypoint = null;
    foreach (var waypoint in Waypoints)
    {
      waypoint.WriteToStream(stream, previousWaypoint);
      previousWaypoint = waypoint;
    }
  }

  /// <summary>
  /// Converts the route to IOF XML 3.0 binary format and returns it as a base64-encoded string.
  /// </summary>
  /// <param name="formattingOptions">The formatting options for the base64-encoded string.</param>
  public string ToBase64String(Base64FormattingOptions formattingOptions = Base64FormattingOptions.None)
  {
    return Convert.ToBase64String(ToByteArray(), formattingOptions);
  }

  /// <summary>
  /// Converts the route to IOF XML 3.0 binary format and returns it as a byte array.
  /// </summary>
  public byte[] ToByteArray()
  {
    using (var ms = new MemoryStream())
    {
      WriteToStream(ms);
      return ms.ToArray();
    }
  }

  /// <summary>
  /// Reads a route in IOF XML 3.0 binary format from a stream.
  /// </summary>
  /// <param name="stream">The stream to read from.</param>
  public static IofXml30Route FromStream(Stream stream)
  {
    var waypoints = new List<IofXml30Waypoint>();
    while (stream.Position < stream.Length)
    {
      waypoints.Add(IofXml30Waypoint.FromStream(stream, waypoints.LastOrDefault()));
    }
    return new IofXml30Route() { Waypoints = waypoints };
  }

  /// <summary>
  /// Reads a route in IOF XML 3.0 binary format from a base64-encoded string.
  /// </summary>
  /// <param name="base64String">The base64-encoded string to read from.</param>
  public static IofXml30Route FromBase64String(string base64String)
  {
    return FromByteArray(Convert.FromBase64String(base64String));
  }

  /// <summary>
  /// Reads a route in IOF XML 3.0 binary format from a byte array.
  /// </summary>
  /// <param name="bytes">The bytes to read from.</param>
  public static IofXml30Route FromByteArray(byte[] bytes)
  {
    using (var ms = new MemoryStream(bytes))
    {
      return FromStream(ms);
    }
  }

  /// <summary>
  /// Gets the length of the route in meters.
  /// </summary>
  public double Length
  {
    get { return length ?? (length = CalculateLength()).Value; }
  }

  /// <summary>
  /// Gets the start time of the route.
  /// </summary>
  public DateTime StartTime
  {
    get { return Waypoints.Any() ? Waypoints.First().Time : DateTime.MinValue; }
  }

  /// <summary>
  /// Gets the end time of the route.
  /// </summary>
  public DateTime EndTime
  {
    get { return Waypoints.Any() ? Waypoints.Last().Time : DateTime.MinValue; }
  }

  /// <summary>
  /// Gets the duration of the route.
  /// </summary>
  public TimeSpan Duration
  {
    get { return EndTime - StartTime; }
  }

  private double CalculateLength()
  {
    var sum = 0.0;
    var wpList = Waypoints.ToList();
    for(var i=1; i<Waypoints.Count(); i++)
    {
      sum += GetDistanceBetweenWaypoints(wpList[i - 1], wpList[i]);
    }
    return sum;
  }

  private static double GetDistanceBetweenWaypoints(IofXml30Waypoint w1, IofXml30Waypoint w2)
  {
    // use spherical coordinates: rho, phi, theta
    const double rho = 6378200; // earth radius in metres

    double sinPhi0 = Math.Sin(0.5 * Math.PI + w1.Latitude / 180.0 * Math.PI);
    double cosPhi0 = Math.Cos(0.5 * Math.PI + w1.Latitude / 180.0 * Math.PI);
    double sinTheta0 = Math.Sin(w1.Longitude / 180.0 * Math.PI);
    double cosTheta0 = Math.Cos(w1.Longitude / 180.0 * Math.PI);

    double sinPhi1 = Math.Sin(0.5 * Math.PI + w2.Latitude / 180.0 * Math.PI);
    double cosPhi1 = Math.Cos(0.5 * Math.PI + w2.Latitude / 180.0 * Math.PI);
    double sinTheta1 = Math.Sin(w2.Longitude / 180.0 * Math.PI);
    double cosTheta1 = Math.Cos(w2.Longitude / 180.0 * Math.PI);

    var x1 = rho * sinPhi0 * cosTheta0;
    var y1 = rho * sinPhi0 * sinTheta0;
    var z1 = rho * cosPhi0;

    var x2 = rho * sinPhi1 * cosTheta1;
    var y2 = rho * sinPhi1 * sinTheta1;
    var z2 = rho * cosPhi1;

    return DistancePointToPoint(x1, y1, z1, x2, y2, z2);
  }

  private static double DistancePointToPoint(double x1, double y1, double z1, double x2, double y2, double z2)
  {
    var sum = (x2 - x1)*(x2 - x1) + (y2 - y1)*(y2 - y1) + (z2 - z1)*(z2 - z1);
    return Math.Sqrt(sum);
  }
}

/// <summary>
/// Class representing a waypoint, including logic for converting to/from an IOF XML 3.0 waypoint stored in binary format.
/// </summary>
public class IofXml30Waypoint
{
  private static readonly DateTime zeroTime = new DateTime(1900, 01, 01, 00, 00, 00, DateTimeKind.Utc);
  private const long timeSecondsThreshold = 255;
  private const long timeMillisecondsThreshold = 65535;
  private const int lanLngBigDeltaLowerThreshold = -32768;
  private const int lanLngBigDeltaUpperThreshold = 32767;
  private const int lanLngSmallDeltaLowerThreshold = -128;
  private const int lanLngSmallDeltaUpperThreshold = 127;
  private const int altitudeDeltaLowerThreshold = -128;
  private const int altitudeDeltaUpperThreshold = 127;

  /// <summary>
  /// Gets or sets the type of the waypoint; normal or interruption.
  /// </summary>
  public IofXml30WaypointType Type { get; set; }

  /// <summary>
  /// Gets or sets the time when the waypoint was recorded.
  /// </summary>
  public DateTime Time { get; set; }

  /// <summary>
  /// Gets or sets the latitude of the waypoint.
  /// </summary>
  public double Latitude { get; set; }

  /// <summary>
  /// Gets or sets the longitude of the waypoint.
  /// </summary>
  public double Longitude { get; set; }

  /// <summary>
  /// Gets or sets the altitude of the waypoint.
  /// </summary>
  public double? Altitude { get; set; }

  /// <summary>
  /// Gets or sets the the time when the waypoint was recorded in the internal storage mode.
  /// </summary>
  public ulong StorageTime
  {
    get { return (ulong)Math.Round((Time - zeroTime).TotalMilliseconds); }
    set { Time = zeroTime.AddMilliseconds(value); }
  }

  /// <summary>
  /// Gets or sets the latitude of the waypoint in the internal storage mode.
  /// </summary>
  public int StorageLatitude
  {
    get { return (int)Math.Round(Latitude * 1000000); }
    set { Latitude = (double)value / 1000000; }
  }

  /// <summary>
  /// Gets or sets the longitude of the waypoint in the internal storage mode.
  /// </summary>
  public int StorageLongitude
  {
    get { return (int)Math.Round(Longitude * 1000000); }
    set { Longitude = (double)value / 1000000; }
  }

  /// <summary>
  /// Gets or sets the altitude of the waypoint in the internal storage mode.
  /// </summary>
  public int? StorageAltitude
  {
    get { return Altitude == null ? (int?)null : (int)Math.Round(Altitude.Value * 10); }
    set { Altitude = value == null ? (double?)null : (double)value / 10; }
  }

  /// <summary>
  /// Writes the waypoint in IOF XML 3.0 binary format to a stream.
  /// </summary>
  /// <param name="stream">The stream to write to.</param>
  /// <param name="previousWaypoint">The previous waypoint of the route, or null if this is the first waypoint.</param>
  public void WriteToStream(Stream stream, IofXml30Waypoint previousWaypoint)
  {
    var timeStorageMode = TimeStorageMode.Full;
    if (previousWaypoint != null)
    {
      if ((StorageTime - previousWaypoint.StorageTime) % 1000 == 0 && (StorageTime - previousWaypoint.StorageTime) / 1000 <= timeSecondsThreshold)
      {
        timeStorageMode = TimeStorageMode.Seconds;
      }
      else if (StorageTime - previousWaypoint.StorageTime <= timeMillisecondsThreshold)
      {
        timeStorageMode = TimeStorageMode.Milliseconds;
      }
    }

    var positionStorageMode = PositionStorageMode.Full;
    if (previousWaypoint != null &&
        (StorageAltitude == null || (previousWaypoint.StorageAltitude != null && StorageAltitude - previousWaypoint.StorageAltitude >= altitudeDeltaLowerThreshold && StorageAltitude - previousWaypoint.StorageAltitude <= altitudeDeltaUpperThreshold)))
    {
      if (StorageLatitude - previousWaypoint.StorageLatitude >= lanLngSmallDeltaLowerThreshold && StorageLatitude - previousWaypoint.StorageLatitude <= lanLngSmallDeltaUpperThreshold &&
          StorageLongitude - previousWaypoint.StorageLongitude >= lanLngSmallDeltaLowerThreshold && StorageLongitude - previousWaypoint.StorageLongitude <= lanLngSmallDeltaUpperThreshold)
      {
        positionStorageMode = PositionStorageMode.SmallDelta;
      }
      else if (StorageLatitude - previousWaypoint.StorageLatitude >= lanLngBigDeltaLowerThreshold && StorageLatitude - previousWaypoint.StorageLatitude <= lanLngBigDeltaUpperThreshold &&
               StorageLongitude - previousWaypoint.StorageLongitude >= lanLngBigDeltaLowerThreshold && StorageLongitude - previousWaypoint.StorageLongitude <= lanLngBigDeltaUpperThreshold)
      {
        positionStorageMode = PositionStorageMode.BigDelta;
      }
    }

    var headerByte = 0;

    if (Type == IofXml30WaypointType.Interruption) headerByte |= (1 << 7);
    if (timeStorageMode == TimeStorageMode.Milliseconds) headerByte |= (1 << 6);
    if (timeStorageMode == TimeStorageMode.Seconds) headerByte |= (1 << 5);
    if (positionStorageMode == PositionStorageMode.BigDelta) headerByte |= (1 << 4);
    if (positionStorageMode == PositionStorageMode.SmallDelta) headerByte |= (1 << 3);
    if (StorageAltitude != null) headerByte |= (1 << 2);

    // header byte
    stream.WriteByte((byte)headerByte);

    // time byte(s)
    switch (timeStorageMode)
    {
      case TimeStorageMode.Full: // 6 bytes
        stream.Write(BitConverter.GetBytes(StorageTime).Reverse().ToArray(), 2, 6);
        break;
      case TimeStorageMode.Milliseconds: // 2 bytes
        stream.Write(BitConverter.GetBytes((ushort)(StorageTime - previousWaypoint.StorageTime)).Reverse().ToArray(), 0, 2);
        break;
      case TimeStorageMode.Seconds: // 1 byte
        stream.WriteByte((byte)((StorageTime - previousWaypoint.StorageTime) / 1000));
        break;
    }

    // position bytes
    switch (positionStorageMode)
    {
      case PositionStorageMode.Full: // 4 + 4 + 3 bytes
        stream.Write(BitConverter.GetBytes(StorageLatitude).Reverse().ToArray(), 0, 4);
        stream.Write(BitConverter.GetBytes(StorageLongitude).Reverse().ToArray(), 0, 4);
        if (StorageAltitude != null) stream.Write(BitConverter.GetBytes(StorageAltitude.Value).Reverse().ToArray(), 1, 3);
        break;
      case PositionStorageMode.BigDelta: // 2 + 2 + 1 bytes
        stream.Write(BitConverter.GetBytes((short)(StorageLatitude - previousWaypoint.StorageLatitude)).Reverse().ToArray(), 0, 2);
        stream.Write(BitConverter.GetBytes((short)(StorageLongitude - previousWaypoint.StorageLongitude)).Reverse().ToArray(), 0, 2);
        if (StorageAltitude != null) stream.Write(BitConverter.GetBytes((sbyte)(StorageAltitude - previousWaypoint.StorageAltitude).Value), 0, 1);
        break;
      case PositionStorageMode.SmallDelta: // 1 + 1 + 1 bytes
        stream.Write(BitConverter.GetBytes((sbyte)(StorageLatitude - previousWaypoint.StorageLatitude)), 0, 1);
        stream.Write(BitConverter.GetBytes((sbyte)(StorageLongitude - previousWaypoint.StorageLongitude)), 0, 1);
        if (StorageAltitude != null) stream.Write(BitConverter.GetBytes((sbyte)(StorageAltitude - previousWaypoint.StorageAltitude).Value), 0, 1);
        break;
    }
  }

  /// <summary>
  /// Reads a waypoint in IOF XML 3.0 binary format from a stream.
  /// </summary>
  /// <param name="stream">The stream to read from.</param>
  /// <param name="previousWaypoint">The previous waypoint of the route, or null if this is the first waypoint.</param>
  /// <returns></returns>
  public static IofXml30Waypoint FromStream(Stream stream, IofXml30Waypoint previousWaypoint)
  {
    var waypoint = new IofXml30Waypoint();

    // header byte
    var headerByte = stream.ReadByte();
    waypoint.Type = (headerByte & (1 << 7)) == 0 ? IofXml30WaypointType.Normal : IofXml30WaypointType.Interruption;
    var timeStorageMode = TimeStorageMode.Full;
    if ((headerByte & (1 << 6)) > 0)
    {
      timeStorageMode = TimeStorageMode.Milliseconds;
    }
    else if ((headerByte & (1 << 5)) > 0)
    {
      timeStorageMode = TimeStorageMode.Seconds;
    }
    var positionStorageMode = PositionStorageMode.Full;
    if ((headerByte & (1 << 4)) > 0)
    {
      positionStorageMode = PositionStorageMode.BigDelta;
    }
    else if ((headerByte & (1 << 3)) > 0)
    {
      positionStorageMode = PositionStorageMode.SmallDelta;
    }
    var altitudePresent = (headerByte & (1 << 2)) > 0;

    byte[] bytes;
    int b;

    // time byte(s)
    switch (timeStorageMode)
    {
      case TimeStorageMode.Full: // 4 bytes
        bytes = new byte[8];
        stream.Read(bytes, 2, 6);
        waypoint.StorageTime = BitConverter.ToUInt64(bytes.Reverse().ToArray(), 0);
        break;
      case TimeStorageMode.Milliseconds: // 2 bytes
        bytes = new byte[2];
        stream.Read(bytes, 0, 2);
        waypoint.StorageTime = previousWaypoint.StorageTime + BitConverter.ToUInt16(bytes.Reverse().ToArray(), 0);
        break;
      case TimeStorageMode.Seconds: // 1 byte
        b = stream.ReadByte();
        waypoint.StorageTime = previousWaypoint.StorageTime + (ulong)b * 1000;
        break;
    }

    // position bytes
    switch (positionStorageMode)
    {
      case PositionStorageMode.Full: // 4 + 4 + 3 bytes
        bytes = new byte[4];
        stream.Read(bytes, 0, 4);
        waypoint.StorageLatitude = BitConverter.ToInt32(bytes.Reverse().ToArray(), 0);
        bytes = new byte[4];
        stream.Read(bytes, 0, 4);
        waypoint.StorageLongitude = BitConverter.ToInt32(bytes.Reverse().ToArray(), 0);
        if (altitudePresent)
        {
          bytes = new byte[4];
          stream.Read(bytes, 1, 3);
          waypoint.StorageAltitude = BitConverter.ToInt32(bytes.Reverse().ToArray(), 0);
        }
        break;
      case PositionStorageMode.BigDelta: // 2 + 2 + 1 bytes
        bytes = new byte[2];
        stream.Read(bytes, 0, 2);
        waypoint.StorageLatitude = previousWaypoint.StorageLatitude + BitConverter.ToInt16(bytes.Reverse().ToArray(), 0);
        bytes = new byte[2];
        stream.Read(bytes, 0, 2);
        waypoint.StorageLongitude = previousWaypoint.StorageLongitude + BitConverter.ToInt16(bytes.Reverse().ToArray(), 0);
        if (altitudePresent)
        {
          b = stream.ReadByte();
          waypoint.StorageAltitude = previousWaypoint.StorageAltitude + (sbyte)b;
        }
        break;
      case PositionStorageMode.SmallDelta: // 1 + 1 + 1 bytes
        b = stream.ReadByte();
        waypoint.StorageLatitude = previousWaypoint.StorageLatitude + (sbyte)b;
        b = stream.ReadByte();
        waypoint.StorageLongitude = previousWaypoint.StorageLongitude + (sbyte)b;
        if (altitudePresent)
        {
          b = stream.ReadByte();
          waypoint.StorageAltitude = previousWaypoint.StorageAltitude + (sbyte)b;
        }
        break;
    }

    return waypoint;
  }

  /// <summary>
  /// The storage mode for the time of a waypoint.
  /// </summary>
  private enum TimeStorageMode
  {
    /// <summary>
    /// The time is stored as a 6-byte unsigned integer, and shows the number of milliseconds since January 1, 1900, 00:00:00 UTC.
    /// </summary>
    Full,

    /// <summary>
    /// The time is stored as a 2-byte unsigned integer, and shows the number of seconds since the previous waypoint's time.
    /// </summary>
    Seconds,

    /// <summary>
    /// The time is stored as a 4-byte unsigned integer, and shows the number of milliseconds since the previous waypoint's time.
    /// </summary>
    Milliseconds
  }

  /// <summary>
  /// The storage mode for the position (latitude, longitude, altitude) of a waypoint.
  /// </summary>
  private enum PositionStorageMode
  {
    /// <summary>
    /// The longitude and latitude are stored as microdegrees in 4-byte signed integers, and the altitude is stored as decimeters in a 3-byte signed integer.
    /// </summary>
    Full,

    /// <summary>
    /// The longitude and latitude are stored as microdegrees relative to the previous waypoint in 2-byte signed integers, and the altitude is stored as decimeters relative to the previous waypoint in a 3-byte signed integer>.
    /// </summary>
    BigDelta,

    /// <summary>
    /// The longitude and latitude are stored as microdegrees relative to the previous waypoint in 1-byte signed integers, and the altitude is stored as decimeters relative to the previous waypoint in a 1-byte signed integer.
    /// </summary>
    SmallDelta
  }
}

/// <summary>
/// The type of waypoint.
/// </summary>
public enum IofXml30WaypointType
{
  /// <summary>
  /// A normal waypoint.
  /// </summary>
  Normal,

  /// <summary>
  /// A waypoint that is the last waypoint before an interruption in the route occurs.
  /// </summary>
  Interruption
}
