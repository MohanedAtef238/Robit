using System;
using NUnit.Framework;

public class ClockLogicTests
{
    private ClockLogic clockLogic;

    [SetUp]
    public void SetUp()
    {
        clockLogic = new ClockLogic();
    }

    [Test]
    public void CalculateAngles_AtMidnight_HourAngleIsZero()
    {
        var time = new DateTime(2026, 1, 1, 0, 0, 0);
        var angles = clockLogic.CalculateAngles(time);
        Assert.AreEqual(0f, angles.hourAngle, 0.01f,
            "Hour 0 (midnight) → (0 % 12) = 0 → 0 * 30 = 0°");
    }

    [Test]
    public void CalculateAngles_AtNoon_HourAngleIsZeroViaMod12()
    {
        var time = new DateTime(2026, 1, 1, 12, 0, 0);
        var angles = clockLogic.CalculateAngles(time);
        Assert.AreEqual(0f, angles.hourAngle, 0.01f,
            "Hour 12 (noon) → (12 % 12) = 0 → 0 * 30 = 0°. Tests the mod-12 branch.");
    }

    [Test]
    public void CalculateAngles_AtSix_ReturnsExpected()
    {
        // 06:00:00 -> Hour should be 180, Minute should be 0
        var time = new DateTime(2026, 1, 1, 6, 0, 0);
        var angles = clockLogic.CalculateAngles(time);

        Assert.AreEqual(180f, angles.hourAngle, 0.01f);
        Assert.AreEqual(0f, angles.minuteAngle, 0.01f);
    }

    [Test]
    public void CalculateAngles_AtThreeFifteen_ReturnsExpected()
    {
        // 03:15:00
        // Minute 15 -> 15 * 6 = 90 degrees
        // Hour 3.25 (since 15/60 = 0.25) -> 3.25 * 30 = 97.5 degrees
        var time = new DateTime(2026, 1, 1, 3, 15, 0);
        var angles = clockLogic.CalculateAngles(time);

        Assert.AreEqual(97.5f, angles.hourAngle, 0.01f);
        Assert.AreEqual(90f, angles.minuteAngle, 0.01f);
    }

    [Test]
    public void GetDigitalTime_FormatsCorrectly()
    {
        Assert.AreEqual("12:00", clockLogic.GetDigitalTime(new DateTime(2026, 1, 1, 0, 0, 0)));
        Assert.AreEqual("12:00", clockLogic.GetDigitalTime(new DateTime(2026, 1, 1, 12, 0, 0)));
        Assert.AreEqual("01:05", clockLogic.GetDigitalTime(new DateTime(2026, 1, 1, 1, 5, 0)));
        Assert.AreEqual("01:05", clockLogic.GetDigitalTime(new DateTime(2026, 1, 1, 13, 5, 0)));
    }

    [Test]
    public void GetAmPm_ReturnsCorrectValue()
    {
        Assert.AreEqual("AM", clockLogic.GetAmPm(new DateTime(2026, 1, 1, 0, 0, 0)));
        Assert.AreEqual("AM", clockLogic.GetAmPm(new DateTime(2026, 1, 1, 11, 59, 59)));
        Assert.AreEqual("PM", clockLogic.GetAmPm(new DateTime(2026, 1, 1, 12, 0, 0)));
        Assert.AreEqual("PM", clockLogic.GetAmPm(new DateTime(2026, 1, 1, 23, 59, 59)));
    }
}
