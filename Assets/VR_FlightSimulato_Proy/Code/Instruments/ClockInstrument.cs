using System;
using UnityEngine;

public class ClockInstrument : MonoBehaviour {
    [SerializeField] private Transform hourHand;
    [SerializeField] private Transform minuteHand;
    [SerializeField] private Transform secondHand;

    void Update() {
        DateTime currentTime = DateTime.Now;

        // Convertir tiempo a ángulos
        float secondAngle = currentTime.Second * 6f; // 360° / 60 = 6° por segundo
        float minuteAngle = currentTime.Minute * 6f; // 6° por minuto
        float hourAngle = (currentTime.Hour % 12) * 30f + currentTime.Minute * 0.5f; // 30° por hora + 0.5° por minuto

        // Aplicar rotaciones (negativo para sentido horario correcto)
        secondHand.localRotation = Quaternion.Euler(0, 0, -secondAngle);
        minuteHand.localRotation = Quaternion.Euler(0, 0, -minuteAngle);
        hourHand.localRotation = Quaternion.Euler(0, 0, -hourAngle);
    }
}
