using System;

public interface IClockLogic
{
    (float hourAngle, float minuteAngle) CalculateAngles(DateTime time);
    string GetDigitalTime(DateTime time);
    string GetAmPm(DateTime time);
}

public class ClockLogic : IClockLogic
{
    public (float hourAngle, float minuteAngle) CalculateAngles(DateTime time)
    {
        float minute = time.Minute + time.Second / 60f;
        float hour = (time.Hour % 12) + minute / 60f;
        return (hour * 30f, minute * 6f);
    }

    public string GetDigitalTime(DateTime time)
    {
        int h = time.Hour % 12;
        if (h == 0) h = 12;
        return $"{h:00}:{time.Minute:00}";
    }

    public string GetAmPm(DateTime time) => time.Hour >= 12 ? "PM" : "AM";
}
