using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(RepairFaultAlarm))]
public class RepairFaultAlarmEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        RepairFaultAlarm alarm = (RepairFaultAlarm)target;
        if (Application.isPlaying)
        {
            string sources = "";
            if (alarm.leak != null && alarm.leak.HasFault) sources += "漏水 ";
            if (alarm.cooling != null && alarm.cooling.HasFault) sources += "冷却 ";
            if (alarm.fuse != null && alarm.fuse.HasFault) sources += "保险丝 ";
            EditorGUILayout.HelpBox("当前报警来源：" + (sources.Length > 0 ? sources : "无"), MessageType.Info);
            if (alarm.cooling != null)
                EditorGUILayout.HelpBox("冷却状态：" + alarm.cooling.RepairStatus, MessageType.Info);
            Repaint();
        }
        SerializedProperty property = serializedObject.GetIterator();
        bool enterChildren = true;
        while (property.NextVisible(enterChildren))
        {
            enterChildren = false;
            using (new EditorGUI.DisabledScope(property.name == "m_Script"))
                EditorGUILayout.PropertyField(property, true);
            switch (property.name)
            {
                case "alarmClip": PreviewButton(alarm, 0, "试听：首次故障提示音"); break;
                case "persistentAlarmClip": PreviewButton(alarm, 1, "试听：持续警报（播放一遍，含淡入）"); break;
                case "repairCompleteClip": PreviewButton(alarm, 2, "试听：配电／冷却启动音"); break;
                case "leakCompleteClip": PreviewButton(alarm, 3, "试听：漏水密封确认音"); break;
            }
        }
        serializedObject.ApplyModifiedProperties();
        using (new EditorGUI.DisabledScope(!Application.isPlaying))
            if (GUILayout.Button("停止试听")) alarm.StopSoundPreview();
        EditorGUILayout.HelpBox("进入 Play 模式后可试听，暂停菜单中也可用。音频槽留空时试听默认音效；试听不会改变故障状态。", MessageType.Info);
    }

    private void PreviewButton(RepairFaultAlarm alarm, int sound, string label)
    {
        using (new EditorGUI.DisabledScope(!Application.isPlaying || !alarm.isActiveAndEnabled))
        {
            if (GUILayout.Button(label))
            {
                serializedObject.ApplyModifiedProperties();
                alarm.PreviewAlarmSound(sound);
            }
        }
    }
}
