using UnityEngine;

public class RepairLeakPrototype : MonoBehaviour
{
    private bool cardOpen;
    private bool bilgeChecked;
    private bool jointChecked;
    private bool toolTaken;
    private bool repaired;

    // 0：等待关阀
    // 1：阀门已关闭
    // 2：密封夹已安装
    // 3：阀门已重新打开，等待复查
    private int repairStep;

    private string message = "Leak warning. Open the job card.";
    private Vector2 scrollPosition;

    private void OnGUI()
    {
        if (GUI.Button(
            new Rect(20, Screen.height - 55, 240, 35),
            cardOpen ? "Close Repair Card" : "Open Repair Card"))
        {
            cardOpen = !cardOpen;
        }

        if (!cardOpen) return;

        float width = Mathf.Min(480f, Screen.width - 20f);
        float height = Mathf.Max(100f, Screen.height - 90f);
        float left = (Screen.width - width) * 0.5f;

        GUILayout.BeginArea(
            new Rect(left, 15f, width, height),
            GUI.skin.box
        );

        scrollPosition = GUILayout.BeginScrollView(scrollPosition);

        GUILayout.Label("JOB CARD 001 - PIPE LEAK");
        GUILayout.Label(repaired ? "STATUS: REPAIRED" : "STATUS: FAULT ACTIVE");

        GUILayout.Space(10);
        GUILayout.Label("CHECK: Inspect the bilge and pipe joint.");

        if (GUILayout.Button(Mark(bilgeChecked) + "1. Inspect bilge"))
            InspectBilge();

        if (GUILayout.Button(Mark(jointChecked) + "2. Inspect pipe joint"))
            InspectJoint();

        GUILayout.Space(10);
        GUILayout.Label("REPAIR: Isolate, seal, restore and verify.");

        if (GUILayout.Button(Mark(toolTaken) + "3. Take sealing kit"))
            TakeTool();

        if (GUILayout.Button(Mark(repairStep >= 1) + "4. Close valve V1"))
            CloseValve();

        if (GUILayout.Button(Mark(repairStep >= 2) + "5. Install sealing clamp"))
            InstallClamp();

        if (GUILayout.Button(Mark(repairStep >= 3) + "6. Reopen valve V1"))
            ReopenValve();

        if (GUILayout.Button(Mark(repaired) + "7. Recheck pipe joint"))
            VerifyRepair();

        GUILayout.Space(15);
        GUILayout.Label("RESULT:");
        GUILayout.Label(message);

        GUILayout.Space(15);

        if (GUILayout.Button("Reset Test"))
            ResetTest();

        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private string Mark(bool completed)
    {
        return completed ? "[DONE] " : "[    ] ";
    }

    private void InspectBilge()
    {
        if (repaired) return;

        bilgeChecked = true;
        message = "Bilge level is rising. Check the pipe joint.";
    }

    private void InspectJoint()
    {
        if (repaired) return;

        jointChecked = true;
        message = "Water is escaping from the pipe joint.";
    }

    private void TakeTool()
    {
        if (repaired) return;

        if (!bilgeChecked || !jointChecked)
        {
            message = "Complete both inspections before choosing the tool.";
            return;
        }

        toolTaken = true;
        message = "Sealing kit ready. Close valve V1.";
    }

    private void CloseValve()
    {
        if (repaired) return;

        if (!toolTaken)
        {
            message = "Inspect the fault and take the sealing kit first.";
            return;
        }

        if (repairStep != 0)
        {
            message = "This step is already complete.";
            return;
        }

        repairStep = 1;
        message = "Valve V1 closed. Install the sealing clamp.";
    }

    private void InstallClamp()
    {
        if (repaired) return;

        if (repairStep != 1)
        {
            message = "Install the clamp after closing V1, before reopening it.";
            return;
        }

        repairStep = 2;
        message = "Clamp installed. Reopen valve V1.";
    }

    private void ReopenValve()
    {
        if (repaired) return;

        if (repairStep != 2)
        {
            message = "Install the sealing clamp before reopening V1.";
            return;
        }

        repairStep = 3;
        message = "Valve V1 reopened. Recheck the joint for leaks.";
    }

    private void VerifyRepair()
    {
        if (repaired) return;

        if (repairStep != 3)
        {
            message = "Finish the repair and reopen V1 before rechecking.";
            return;
        }

        repaired = true;
        message = "No leakage detected. Repair complete.";
    }

    private void ResetTest()
    {
        bilgeChecked = false;
        jointChecked = false;
        toolTaken = false;
        repaired = false;
        repairStep = 0;
        message = "Leak warning. Open the job card.";
    }
}