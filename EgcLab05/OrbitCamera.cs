using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace Egc;

public class OrbitCamera : Camera
{
    private Vector3 _position;
    public override Vector3 Position => _position;
    public override Vector3 Front => Vector3.Normalize(Target - _position);

    public Vector3 Target { get; private set; }
    public float Distance { get; private set; }
    public float Azimuth { get; private set; }   // grade (în jurul axei Y)
    public float Elevation { get; private set; } // grade (deasupra planului orizontal)

    public const float MinDistance = 1.5f;
    public const float MaxDistance = 50.0f;
    public const float MinElevation = -89.0f;
    public const float MaxElevation = 89.0f;

    public float MouseSensitivity = 0.25f;
    public float KeyboardRotateSpeed = 90.0f;
    public float ZoomSpeed = 1.0f;
    public float MoveTargetSpeed = 5.0f;

    private static readonly Vector3 StartTarget = Vector3.Zero;
    private const float StartDistance = 8.0f;
    private const float StartAzimuth = 45.0f;
    private const float StartElevation = 30.0f;

    public OrbitCamera()
    {
        Reset();
    }

    public override string Name => "Orbită";

    public override Matrix4 ViewMatrix =>
        Matrix4.LookAt(_position, Target, Vector3.UnitY);

    private void UpdatePosition()
    {
        float azRad = MathHelper.DegreesToRadians(Azimuth);
        float elRad = MathHelper.DegreesToRadians(Elevation);

        Vector3 offset = new Vector3(
            MathF.Cos(elRad) * MathF.Cos(azRad),
            MathF.Sin(elRad),
            MathF.Cos(elRad) * MathF.Sin(azRad)
        ) * Distance;

        _position = Target + offset;
    }

    public override void Reset()
    {
        Target = StartTarget;
        Distance = StartDistance;
        Azimuth = StartAzimuth;
        Elevation = StartElevation;
        FieldOfView = 60.0f;
        UpdatePosition();
    }

    // Bonus 1: FPS -> Orbită fără salt
    public void SetFromFps(Vector3 fpsPosition, Vector3 fpsFront, float fixedDistance = 5.0f)
    {
        Target = fpsPosition + fpsFront * fixedDistance;
        Distance = Math.Clamp(fixedDistance, MinDistance, MaxDistance);

        Vector3 o = -Vector3.Normalize(fpsFront);
        Elevation = Math.Clamp(MathHelper.RadiansToDegrees(MathF.Asin(Math.Clamp(o.Y, -1.0f, 1.0f))), MinElevation, MaxElevation);
        Azimuth = MathHelper.RadiansToDegrees(MathF.Atan2(o.Z, o.X));

        UpdatePosition();
    }

    public override void Update(KeyboardState keyboard, MouseState mouse, Vector2 mouseDelta, float dt)
    {
        // 1. Rotație cu mouse-ul (clic stânga ținut apăsat)
        if (mouse.IsButtonDown(MouseButton.Left))
        {
            Azimuth += mouseDelta.X * MouseSensitivity;
            Elevation += mouseDelta.Y * MouseSensitivity;
        }

        // 2. Rotație de la tastatură (Săgeți)
        if (keyboard.IsKeyDown(Keys.Left)) Azimuth -= KeyboardRotateSpeed * dt;
        if (keyboard.IsKeyDown(Keys.Right)) Azimuth += KeyboardRotateSpeed * dt;
        if (keyboard.IsKeyDown(Keys.Up)) Elevation += KeyboardRotateSpeed * dt;
        if (keyboard.IsKeyDown(Keys.Down)) Elevation -= KeyboardRotateSpeed * dt;

        Elevation = Math.Clamp(Elevation, MinElevation, MaxElevation);

        // 3. Zoom cu rotița mouse-ului (schimbă distanța, nu FOV-ul)
        if (mouse.ScrollDelta.Y != 0)
        {
            Distance -= mouse.ScrollDelta.Y * ZoomSpeed;
            Distance = Math.Clamp(Distance, MinDistance, MaxDistance);
        }

        // 4. Bonus 2: W A S D mută ținta în planul orizontal (XZ)
        float azRad = MathHelper.DegreesToRadians(Azimuth);
        Vector3 forwardHoriz = new Vector3(-MathF.Cos(azRad), 0.0f, -MathF.Sin(azRad));
        if (forwardHoriz.LengthSquared > 0.0001f)
            forwardHoriz.Normalize();

        Vector3 rightHoriz = Vector3.Normalize(Vector3.Cross(forwardHoriz, Vector3.UnitY));

        Vector3 move = Vector3.Zero;
        if (keyboard.IsKeyDown(Keys.W)) move += forwardHoriz;
        if (keyboard.IsKeyDown(Keys.S)) move -= forwardHoriz;
        if (keyboard.IsKeyDown(Keys.D)) move += rightHoriz;
        if (keyboard.IsKeyDown(Keys.A)) move -= rightHoriz;

        if (move.LengthSquared > 0)
        {
            move = Vector3.Normalize(move);
            Target += move * MoveTargetSpeed * dt;
        }

        UpdatePosition();
    }

    public override IEnumerable<string> Describe()
    {
        yield return $"Țintă          : ({Target.X,6:F2}, {Target.Y,6:F2}, {Target.Z,6:F2})";
        yield return $"Distanță       : {Distance,6:F2}";
        yield return $"Azimut / elev. : {Azimuth,7:F1}° / {Elevation,6:F1}°";
        yield return $"Poziție        : ({_position.X,6:F2}, {_position.Y,6:F2}, {_position.Z,6:F2})";
    }
}