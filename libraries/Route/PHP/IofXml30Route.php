<?php
  // Classes for managing IOF XML 3.0 Route element.
// IOF XML 3.0 specification: https://github.com/international-orienteering-federation/datastandard-v3.
// IOF XML 3.0 example result list file with Route element: https://github.com/international-orienteering-federation/datastandard-v3/blob/master/examples/ResultList1.xml.

  class IofXml30Route
  {
    const rho = 6378200; // earth radius in metres
    private $length;
    public $waypoints = array();

    public function writeToStream($stream)
    {
      $previousWaypoint = null;
      foreach ($this->waypoints as $waypoint)
      {
        $waypoint->writeToStream($stream, $previousWaypoint);
        $previousWaypoint = $waypoint;
      }
    }

    public function toBase64String()
    {
      return base64_encode($this->toByteArray());
    }

    public function toByteArray()
    {
      $limit = 32*1024*1024;
      $stream = fopen("php://temp/maxmemory:$limit", 'r+');
      $this->writeToStream($stream);
      rewind($stream);
      return stream_get_contents($stream);
    }

    public static function fromStream($stream)
    {
      $waypoints = array();
      $stat = fstat($stream);
      while (ftell($stream) < $stat["size"])
      {
        $waypoints[] = IofXml30Waypoint::fromStream($stream, count($waypoints) == 0 ? null : $waypoints[count($waypoints)-1]);
      }
      $route = new IofXml30Route();
      $route->waypoints = $waypoints;

      return $route;
    }

    public static function fromBase64String($base64String)
    {
      return self::fromByteArray(base64_decode($base64String));
    }

    public static function fromByteArray($bytes)
    {
      $limit = 32*1024*1024;
      $stream = fopen("php://temp/maxmemory:$limit", 'w+');
      fwrite($stream, $bytes);
      rewind($stream);
      return self::fromStream($stream);
    }

    public function getLength()
    {
      if(is_null($this->length)) $this->length = $this->calculateLength();
      return $this->length;
    }

    public function getStartTime()
    {
      return count($this->waypoints) > 0 ? $this->waypoints[0]->time : 0;
    }

    public function getEndTime()
    {
      return count($this->waypoints) > 0 ? $this->waypoints[count($this->waypoints)-1]->time : 0;
    }

    public function getDuration()
    {
      return $this->getEndTime() - $this->getStartTime();
    }

    private function calculateLength()
    {
      $sum = 0.0;
      for($i=1; $i<count($this->waypoints); $i++)
      {
        $sum += $this->getDistanceBetweenWaypoints($this->waypoints[$i - 1], $this->waypoints[$i]);
      }
      return $sum;
    }

    private static function getDistanceBetweenWaypoints($w1, $w2)
    {
      $sinPhi0 = sin(0.5 * M_PI + $w1->latitude / 180.0 * M_PI);
      $cosPhi0 = cos(0.5 * M_PI + $w1->latitude / 180.0 * M_PI);
      $sinTheta0 = sin($w1->longitude / 180.0 * M_PI);
      $cosTheta0 = cos($w1->longitude / 180.0 * M_PI);

      $sinPhi1 = sin(0.5 * M_PI + $w2->latitude / 180.0 * M_PI);
      $cosPhi1 = cos(0.5 * M_PI + $w2->latitude / 180.0 * M_PI);
      $sinTheta1 = sin($w2->longitude / 180.0 * M_PI);
      $cosTheta1 = cos($w2->longitude / 180.0 * M_PI);

      $x1 = self::rho * $sinPhi0 * $cosTheta0;
      $y1 = self::rho * $sinPhi0 * $sinTheta0;
      $z1 = self::rho * $cosPhi0;

      $x2 = self::rho * $sinPhi1 * $cosTheta1;
      $y2 = self::rho * $sinPhi1 * $sinTheta1;
      $z2 = self::rho * $cosPhi1;

      return self::getDistancePointToPoint($x1, $y1, $z1, $x2, $y2, $z2);
    }

    private static function getDistancePointToPoint($x1, $y1, $z1, $x2, $y2, $z2)
    {
      $sum = ($x2 - $x1)*($x2 - $x1) + ($y2 - $y1)*($y2 - $y1) + ($z2 - $z1)*($z2 - $z1);
      return sqrt($sum);
    }

  }

  class IofXml30Waypoint
  {
    const zeroTime = 0; //  mktime(0, 0, 0, 1900, 1, 1);
    const timeSecondsThreshold = 255;
    const timeMillisecondsThreshold = 65535;
    const lanLngBigDeltaLowerThreshold = -32768;
    const lanLngBigDeltaUpperThreshold = 32767;
    const lanLngSmallDeltaLowerThreshold = -128;
    const lanLngSmallDeltaUpperThreshold = 127;
    const altitudeDeltaLowerThreshold = -128;
    const altitudeDeltaUpperThreshold = 127;

    public $type;
    public $time; // milliseconds since January 1, 1900, 00:00:00 UTC
    public $longitude;
    public $latitude;
    public $altitude;

    public function getStorageTime()
    {
      return round($this->time - self::zeroTime);
    }

    public function setStorageTime($value)
    {
      $this->time = self::zeroTime + $value;
    }

    public function getStorageLatitude()
    {
      return round($this->latitude * 1000000);
    }

    public function setStorageLatitude($value)
    {
      $this->latitude = $value / 1000000;
    }

    public function getStorageLongitude()
    {
      return round($this->longitude * 1000000);
    }

    public function setStorageLongitude($value)
    {
      $this->longitude = $value / 1000000;
    }

    public function getStorageAltitude()
    {
      return is_null($this->altitude) ? null : round($this->altitude * 10);
    }

    public function setStorageAltitude($value)
    {
      $this->altitude = is_null($value) ? null : $value / 10;
    }

    public function writeToStream($stream, $previousWaypoint)
    {
      $timeStorageMode = IofXml30TimeStorageMode::full;
      if (!is_null($previousWaypoint))
      {
        if (($this->getStorageTime() - $previousWaypoint->getStorageTime())%1000 == 0 &&
            ($this->getStorageTime() - $previousWaypoint->getStorageTime()) / 1000 <= self::timeSecondsThreshold)
        {
          $timeStorageMode = IofXml30TimeStorageMode::seconds;
        }
        else if ($this->getStorageTime() - $previousWaypoint->getStorageTime() <= self::timeMillisecondsThreshold)
        {
          $timeStorageMode = IofXml30TimeStorageMode::milliseconds;
        }
      }

      $positionStorageMode = IofXml30PositionStorageMode::full;
      if (!is_null($previousWaypoint) &&
          (is_null($this->getStorageAltitude()) || (!is_null($previousWaypoint->getStorageAltitude()) && $this->getStorageAltitude() - $previousWaypoint->getStorageAltitude() >= self::altitudeDeltaLowerThreshold && $this->getStorageAltitude() - $previousWaypoint->getStorageAltitude() <= self::altitudeDeltaUpperThreshold)))
      {
        if ($this->getStorageLatitude() - $previousWaypoint->getStorageLatitude() >= self::lanLngSmallDeltaLowerThreshold && $this->getStorageLatitude() - $previousWaypoint->getStorageLatitude() <= self::lanLngSmallDeltaUpperThreshold &&
            $this->getStorageLongitude() - $previousWaypoint->getStorageLongitude() >= self::lanLngSmallDeltaLowerThreshold && $this->getStorageLongitude() - $previousWaypoint->getStorageLongitude() <= self::lanLngSmallDeltaUpperThreshold)
        {
          $positionStorageMode = IofXml30PositionStorageMode::smallDelta;
        }
        else if ($this->getStorageLatitude() - $previousWaypoint->getStorageLatitude() >= self::lanLngBigDeltaLowerThreshold && $this->getStorageLatitude() - $previousWaypoint->getStorageLatitude() <= self::lanLngBigDeltaUpperThreshold &&
                 $this->getStorageLongitude() - $previousWaypoint->getStorageLongitude() >= self::lanLngBigDeltaLowerThreshold && $this->getStorageLongitude() - $previousWaypoint->getStorageLongitude() <= self::lanLngBigDeltaUpperThreshold)
        {
          $positionStorageMode = IofXml30PositionStorageMode::bigDelta;
        }
      }

      $headerByte = 0;

      if ($this->type == IofXml30WaypointType::interruption) $headerByte |= (1 << 7);
      if ($timeStorageMode == IofXml30TimeStorageMode::milliseconds) $headerByte |= (1 << 6);
      if ($timeStorageMode == IofXml30TimeStorageMode::seconds) $headerByte |= (1 << 5);
      if ($positionStorageMode == IofXml30PositionStorageMode::bigDelta) $headerByte |= (1 << 4);
      if ($positionStorageMode == IofXml30PositionStorageMode::smallDelta) $headerByte |= (1 << 3);
      if (!is_null($this->getStorageAltitude())) $headerByte |= (1 << 2);

      // header byte
      self::writeIntegerValue($stream, $headerByte, 1, false);

      // time byte(s)
      switch($timeStorageMode)
      {
        case IofXml30TimeStorageMode::full: // 6 bytes
          self::writeIntegerValue($stream, $this->getStorageTime(), 6, false);
          break;
        case IofXml30TimeStorageMode::milliseconds: // 2 bytes
          self::writeIntegerValue($stream, $this->getStorageTime() - $previousWaypoint->getStorageTime(), 2, false);
          break;
        case IofXml30TimeStorageMode::seconds: // 1 byte
          self::writeIntegerValue($stream, ($this->getStorageTime() - $previousWaypoint->getStorageTime()) / 1000, 1, false);
          break;
      }

      // position bytes
      switch ($positionStorageMode)
      {
        case IofXml30PositionStorageMode::full: // 4 + 4 + 3 bytes
          self::writeIntegerValue($stream, $this->getStorageLatitude(), 4, true);
          self::writeIntegerValue($stream, $this->getStorageLongitude(), 4, true);
          if (!is_null($this->getStorageAltitude())) self::writeIntegerValue($stream, $this->getStorageAltitude(), 3, true);
          break;
        case IofXml30PositionStorageMode::bigDelta: // 2 + 2 + 1 bytes
          self::writeIntegerValue($stream, $this->getStorageLatitude() - $previousWaypoint->getStorageLatitude(), 2, true);
          self::writeIntegerValue($stream, $this->getStorageLongitude() - $previousWaypoint->getStorageLongitude(), 2, true);
          if (!is_null($this->getStorageAltitude())) self::writeIntegerValue($stream, $this->getStorageAltitude() - $previousWaypoint->getStorageAltitude(), 1, true);
          break;
        case IofXml30PositionStorageMode::smallDelta: // 1 + 1 + 1 bytes
          self::writeIntegerValue($stream, $this->getStorageLatitude() - $previousWaypoint->getStorageLatitude(), 1, true);
          self::writeIntegerValue($stream, $this->getStorageLongitude() - $previousWaypoint->getStorageLongitude(), 1, true);
          if (!is_null($this->getStorageAltitude())) self::writeIntegerValue($stream, $this->getStorageAltitude() - $previousWaypoint->getStorageAltitude(), 1, true);
          break;
      }
    }

    public static function fromStream($stream, $previousWaypoint)
    {
      $waypoint = new IofXml30Waypoint();

      // header byte
      $headerByte = self::readIntegerValue($stream, 1, false);
      $waypoint->type = ($headerByte & (1 << 7)) == 0 ? IofXml30WaypointType::normal : IofXml30WaypointType::interruption;
      $timeStorageMode = IofXml30TimeStorageMode::full;
      if (($headerByte & (1 << 6)) > 0)
      {
        $timeStorageMode = IofXml30TimeStorageMode::milliseconds;
      }
      else if (($headerByte & (1 << 5)) > 0)
      {
        $timeStorageMode = IofXml30TimeStorageMode::seconds;
      }
      $positionStorageMode = IofXml30PositionStorageMode::full;
      if (($headerByte & (1 << 4)) > 0)
      {
        $positionStorageMode = IofXml30PositionStorageMode::bigDelta;
      }
      else if (($headerByte & (1 << 3)) > 0)
      {
        $positionStorageMode = IofXml30PositionStorageMode::smallDelta;
      }
      $altitudePresent = ($headerByte & (1 << 2)) > 0;

      // time byte(s)
      switch($timeStorageMode)
      {
        case IofXml30TimeStorageMode::full: // 6 bytes
          $waypoint->setStorageTime(self::readIntegerValue($stream, 6, false));
          break;
        case IofXml30TimeStorageMode::milliseconds: // 2 bytes
          $waypoint->setStorageTime($previousWaypoint->getStorageTime() + self::readIntegerValue($stream, 2, false));
          break;
        case IofXml30TimeStorageMode::seconds: // 1 byte
          $waypoint->setStorageTime($previousWaypoint->getStorageTime() + self::readIntegerValue($stream, 1, false) * 1000);
          break;
      }

      // position bytes
      switch ($positionStorageMode)
      {
        case IofXml30PositionStorageMode::full: // 4 + 4 + 3 bytes
          $waypoint->setStorageLatitude(self::readIntegerValue($stream, 4, true));
          $waypoint->setStorageLongitude(self::readIntegerValue($stream, 4, true));
          if($altitudePresent)
          {
            $waypoint->setStorageAltitude(self::readIntegerValue($stream, 3, true));
          }
          break;
        case IofXml30PositionStorageMode::bigDelta: // 2 + 2 + 1 bytes
          $waypoint->setStorageLatitude($previousWaypoint->getStorageLatitude() + self::readIntegerValue($stream, 2, true));
          $waypoint->setStorageLongitude($previousWaypoint->getStorageLongitude() + self::readIntegerValue($stream, 2, true));
          if($altitudePresent)
          {
            $waypoint->setStorageAltitude($previousWaypoint->getStorageAltitude() + self::readIntegerValue($stream, 1, true));
          }
          break;
        case IofXml30PositionStorageMode::smallDelta: // 1 + 1 + 1 bytes
          $waypoint->setStorageLatitude($previousWaypoint->getStorageLatitude() + self::readIntegerValue($stream, 1, true));
          $waypoint->setStorageLongitude($previousWaypoint->getStorageLongitude() + self::readIntegerValue($stream, 1, true));
          if($altitudePresent)
          {
            $waypoint->setStorageAltitude($previousWaypoint->getStorageAltitude() + self::readIntegerValue($stream, 1, true));
          }
          break;
      }

      return $waypoint;
    }

    private static function readIntegerValue($stream, $byteCount, $signed)
    {
      $data = fread($stream, $byteCount);
      $bitCount = $byteCount * 8;
      $value = 0;
      $multiplier = 1;
      for($i=$byteCount-1; $i>=0; $i--)
      {
        if(isset($data[$i])) $value += $multiplier * ord($data[$i]);
        $multiplier *= 1 << 8;
      }
      if($signed && isset($data[0]) && (ord($data[0]) & (1 << 7)))
      {
        $value = -pow(2, $bitCount) + $value;
      }
      return $value;
    }

    private static function writeIntegerValue($stream, $value, $byteCount, $signed)
    {
      $bitCount = $byteCount * 8;
      if($signed)
      {
        if($value < -pow(2, $bitCount-1)) $value = -pow(2, $bitCount-1);
        if($value > pow(2, $bitCount-1)-1) $value = pow(2, $bitCount-1)-1;
      }
      else
      {
        if($value < 0) $value = 0;
        if($value > pow(2, $bitCount)-1) $value = pow(2, $bitCount-1);
      }

      if($signed && $value < 0) $value = pow(2, $bitCount) + $value;
      $data = "";
      for($i=0; $i<$byteCount; $i++)
      {
        $data = chr($value % 256) . $data;
        $value = ($value - $value % 256) / 256;
      }
      fwrite($stream, $data);
    }
  }

  class IofXml30TimeStorageMode
  {
    const full = 0;
    const seconds = 1;
    const milliseconds = 2;
  }

  class IofXml30PositionStorageMode
  {
    const full = 0;
    const bigDelta = 1;
    const smallDelta = 2;
  }

  class IofXml30WaypointType
  {
    const normal = 0;
    const interruption = 1;
  }
?>
