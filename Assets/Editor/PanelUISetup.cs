using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using VectorFieldUI;

/// <summary>
/// Adds the missing dropScaleX, dropScaleY, and dropZone UI dropdowns
/// to the Panel prefab and wires them to PanelController.
/// Run once: Menu → Panel → Add Missing Dropdowns
/// </summary>
public class PanelUISetup
{
    [MenuItem("Panel/Add Missing Dropdowns")]
    public static void AddMissingDropdowns()
    {
        const string prefabPath = "Assets/Prefabs/Panel.prefab";

        using var scope = new PrefabUtility.EditPrefabContentsScope(prefabPath);
        GameObject root = scope.prefabContentsRoot;

        PanelController pc = root.GetComponentInChildren<PanelController>(true);
        if (pc == null) { Debug.LogError("[PanelSetup] PanelController not found."); return; }

        EnsureMissingDropdowns(pc);
        BeautifyPanelLayout(pc);
        RemoveConflictingTrackedRaycasters(root);
        Debug.Log("[PanelSetup] Done! Rebuild/run to populate dropdown options via PanelController.InitDropdowns.");
    }

    [MenuItem("Panel/Fix Panel Prefab (All)")]
    public static void FixPanelPrefabAll()
    {
        AddMissingDropdowns();
    }

    [MenuItem("Panel/Fix Panel In Open Scenes")]
    public static void FixPanelInOpenScenes()
    {
        bool changed = false;

        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
        {
            var scene = EditorSceneManager.GetSceneAt(i);
            if (!scene.isLoaded)
                continue;

            foreach (var root in scene.GetRootGameObjects())
            {
                var controllers = root.GetComponentsInChildren<PanelController>(true);
                foreach (var pc in controllers)
                {
                    if (pc == null)
                        continue;

                    Undo.RecordObject(pc, "Fix PanelController refs");
                    bool localChange = EnsureMissingDropdowns(pc);
                    localChange |= BeautifyPanelLayout(pc);
                    localChange |= RemoveConflictingTrackedRaycasters(pc.gameObject);
                    if (localChange)
                    {
                        EditorUtility.SetDirty(pc);
                        changed = true;
                    }
                }
            }

            if (changed)
                EditorSceneManager.MarkSceneDirty(scene);
        }

        if (changed)
            Debug.Log("[PanelSetup] Open scenes fixed (dropdowns + raycasters). Save scenes to persist changes.");
        else
            Debug.Log("[PanelSetup] No scene panel changes were needed.");
    }

    static bool EnsureMissingDropdowns(PanelController pc)
    {
        bool changed = false;
        TMP_Dropdown existingDrop = pc.dropCount ?? pc.dropFormula;
        if (existingDrop == null)
        {
            Debug.LogError("[PanelSetup] No existing dropdown found to clone.", pc);
            return false;
        }

        Transform content = existingDrop.transform.parent;

        if (pc.dropScaleX == null)
        {
            pc.dropScaleX = FindOrCreateDropdown(content, "DropdownScaleX", "Multiplicador X");
            changed = true;
            Debug.Log("[PanelSetup] dropScaleX created/linked.");
        }

        if (pc.dropScaleY == null)
        {
            pc.dropScaleY = FindOrCreateDropdown(content, "DropdownScaleY", "Multiplicador Y");
            changed = true;
            Debug.Log("[PanelSetup] dropScaleY created/linked.");
        }

        if (pc.dropZone == null)
        {
            pc.dropZone = FindOrCreateDropdown(content, "DropdownZone", "Zona");
            changed = true;
            Debug.Log("[PanelSetup] dropZone created/linked.");
        }

        if (content is RectTransform rt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

        return changed;
    }

    static bool BeautifyPanelLayout(PanelController pc)
    {
        bool changed = false;

        var panelCanvasRt = pc.GetComponent<RectTransform>();
        if (panelCanvasRt != null)
        {
            var targetSize = new Vector2(Mathf.Max(panelCanvasRt.sizeDelta.x, 420f), 760f);
            if (panelCanvasRt.sizeDelta != targetSize)
            {
                panelCanvasRt.sizeDelta = targetSize;
                changed = true;
            }
        }

        Transform content = pc.dropCount != null ? pc.dropCount.transform.parent : null;
        if (content == null)
            return changed;

        EnsureDropdownVisualSafety(pc.dropCount, ref changed);
        EnsureDropdownVisualSafety(pc.dropFormula, ref changed);
        EnsureDropdownVisualSafety(pc.dropScaleX, ref changed);
        EnsureDropdownVisualSafety(pc.dropScaleY, ref changed);
        EnsureDropdownVisualSafety(pc.dropZone, ref changed);

        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            if (vlg.spacing != 8f)
            {
                vlg.spacing = 8f;
                changed = true;
            }

            if (vlg.padding.top != 16 || vlg.padding.bottom != 16 || vlg.padding.left != 16 || vlg.padding.right != 16)
            {
                vlg.padding.top = 16;
                vlg.padding.bottom = 16;
                vlg.padding.left = 16;
                vlg.padding.right = 16;
                changed = true;
            }

            if (!vlg.childControlWidth || !vlg.childControlHeight || !vlg.childForceExpandWidth)
            {
                vlg.childControlWidth = true;
                vlg.childControlHeight = true;
                vlg.childForceExpandWidth = true;
                vlg.childForceExpandHeight = false;
                changed = true;
            }
        }

        var titleLabel = EnsureTitleLabel(content, ref changed);
        var descriptionLabel = EnsureDescriptionLabel(content, ref changed);
        var sectionFunctions = EnsureSimpleLabel(content, "FunctionsSectionLabel", "Funciones", 20f, FontStyles.Bold, ref changed);

        var countLabel = EnsureLabelForDropdown(content, pc.dropCount, "CountFunctionLabel", "Cantidad de vectores", ref changed);
        var formulaLabel = EnsureLabelForDropdown(content, pc.dropFormula, "FormulaFunctionLabel", "Formula del vector", ref changed);
        var scaleXLabel = EnsureLabelForDropdown(content, pc.dropScaleX, "DropdownScaleX_Label", "Multiplicador X", ref changed);
        var scaleYLabel = EnsureLabelForDropdown(content, pc.dropScaleY, "DropdownScaleY_Label", "Multiplicador Y", ref changed);
        var zoneLabel = EnsureLabelForDropdown(content, pc.dropZone, "DropdownZone_Label", "Zona", ref changed);

        changed |= DisableLegacyDuplicateLabels(content, titleLabel, descriptionLabel, sectionFunctions, countLabel, formulaLabel, scaleXLabel, scaleYLabel, zoneLabel);

        int insertIndex = 0;
        insertIndex = PlaceBlock(content, titleLabel != null ? titleLabel.transform : null, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, descriptionLabel != null ? descriptionLabel.transform : null, insertIndex, ref changed);

        if (pc.statusLabel != null)
            insertIndex = PlaceBlock(content, pc.statusLabel.transform, insertIndex, ref changed);

        insertIndex = PlaceBlock(content, sectionFunctions != null ? sectionFunctions.transform : null, insertIndex, ref changed);

        insertIndex = PlaceBlock(content, countLabel != null ? countLabel.transform : null, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, pc.dropCount != null ? pc.dropCount.transform : null, insertIndex, ref changed);

        insertIndex = PlaceBlock(content, formulaLabel != null ? formulaLabel.transform : null, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, pc.dropFormula != null ? pc.dropFormula.transform : null, insertIndex, ref changed);

        insertIndex = PlaceBlock(content, scaleXLabel != null ? scaleXLabel.transform : null, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, pc.dropScaleX != null ? pc.dropScaleX.transform : null, insertIndex, ref changed);

        insertIndex = PlaceBlock(content, scaleYLabel != null ? scaleYLabel.transform : null, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, pc.dropScaleY != null ? pc.dropScaleY.transform : null, insertIndex, ref changed);

        insertIndex = PlaceBlock(content, zoneLabel != null ? zoneLabel.transform : null, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, pc.dropZone != null ? pc.dropZone.transform : null, insertIndex, ref changed);

        var spacer = content.Find("ButtonsSpacer");
        if (spacer == null)
        {
            var spacerGO = new GameObject("ButtonsSpacer", typeof(RectTransform), typeof(LayoutElement));
            spacerGO.transform.SetParent(content, false);
            spacer = spacerGO.transform;
            changed = true;
        }

        var spacerLE = spacer.GetComponent<LayoutElement>();
        if (spacerLE.preferredHeight != 16f)
        {
            spacerLE.preferredHeight = 16f;
            changed = true;
        }

        if (spacerLE.flexibleHeight != 0f)
        {
            spacerLE.flexibleHeight = 0f;
            changed = true;
        }

        if (spacer.GetSiblingIndex() != insertIndex)
        {
            spacer.SetSiblingIndex(insertIndex);
            changed = true;
        }

        var row = content.Find("ButtonsRow");
        if (row == null)
        {
            var rowGO = new GameObject("ButtonsRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            rowGO.transform.SetParent(content, false);
            row = rowGO.transform;

            var rowLayout = rowGO.GetComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 8f;
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandWidth = true;
            rowLayout.childForceExpandHeight = false;

            var rowLE = rowGO.GetComponent<LayoutElement>();
            rowLE.preferredHeight = 42f;
            rowLE.flexibleWidth = 1f;
            changed = true;
        }

        changed |= MoveButtonToRow(pc.btnGenerate, row);
        changed |= MoveButtonToRow(pc.btnReset, row);
        changed |= MoveButtonToRow(pc.btnDelete, row);

        if (row.GetSiblingIndex() != content.childCount - 1)
        {
            row.SetSiblingIndex(content.childCount - 1);
            changed = true;
        }

        if (content is RectTransform contentRt)
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentRt);

        return changed;
    }

    static void EnsureDropdownVisualSafety(TMP_Dropdown dropdown, ref bool changed)
    {
        if (dropdown == null)
            return;

        if (dropdown.template != null && dropdown.template.gameObject.activeSelf)
        {
            dropdown.template.gameObject.SetActive(false);
            changed = true;
        }

        var rt = dropdown.GetComponent<RectTransform>();
        if (rt != null)
        {
            if (rt.anchorMin != new Vector2(0f, 0.5f) || rt.anchorMax != new Vector2(1f, 0.5f))
            {
                rt.anchorMin = new Vector2(0f, 0.5f);
                rt.anchorMax = new Vector2(1f, 0.5f);
                changed = true;
            }

            if (rt.sizeDelta.y < 34f)
            {
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 38f);
                changed = true;
            }
        }

        var le = dropdown.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = dropdown.gameObject.AddComponent<LayoutElement>();
            changed = true;
        }

        if (le.preferredHeight != 38f)
        {
            le.preferredHeight = 38f;
            changed = true;
        }

        if (le.flexibleWidth != 1f)
        {
            le.flexibleWidth = 1f;
            changed = true;
        }
    }

    static TextMeshProUGUI EnsureTitleLabel(Transform content, ref bool changed)
    {
        var title = FindDirectLabel(content, "PanelTitleLabel");
        if (title == null)
        {
            // Reuse a legacy label if available.
            var legacy = FindDirectLabel(content, "Lbl");
            if (legacy != null)
            {
                legacy.name = "PanelTitleLabel";
                title = legacy;
                changed = true;
            }
        }

        title = EnsureSimpleLabel(content, "PanelTitleLabel", "CAMPO VECTORIAL", 28f, FontStyles.Bold, ref changed, title);
        return title;
    }

    static TextMeshProUGUI EnsureDescriptionLabel(Transform content, ref bool changed)
    {
        var description = FindDirectLabel(content, "PanelDescriptionLabel");
        if (description == null)
        {
            var direct = content.GetComponentsInChildren<TextMeshProUGUI>(true);
            foreach (var candidate in direct)
            {
                if (candidate == null || candidate.transform.parent != content)
                    continue;

                var txt = candidate.text == null ? string.Empty : candidate.text.ToLowerInvariant();
                if (txt.Contains("ajusta") || txt.Contains("formula") || txt.Contains("zona"))
                {
                    candidate.name = "PanelDescriptionLabel";
                    description = candidate;
                    changed = true;
                    break;
                }
            }
        }

        description = EnsureSimpleLabel(
            content,
            "PanelDescriptionLabel",
            "Primero selecciona la cantidad y la formula. Luego ajusta multiplicadores y zona.",
            14f,
            FontStyles.Normal,
            ref changed,
            description);

        return description;
    }

    static TextMeshProUGUI EnsureLabelForDropdown(Transform content, TMP_Dropdown dropdown, string labelName, string labelText, ref bool changed)
    {
        if (dropdown == null)
            return null;

        var label = FindDirectLabel(content, labelName);
        if (label == null)
        {
            int targetIdx = Mathf.Max(dropdown.transform.GetSiblingIndex() - 1, 0);
            if (targetIdx < content.childCount)
            {
                var previous = content.GetChild(targetIdx);
                if (previous != null && previous.parent == content)
                {
                    var prevText = previous.GetComponent<TextMeshProUGUI>();
                    if (prevText != null)
                    {
                        prevText.name = labelName;
                        label = prevText;
                        changed = true;
                    }
                }
            }
        }

        label = EnsureSimpleLabel(content, labelName, labelText, 17f, FontStyles.Bold, ref changed, label);
        return label;
    }

    static TextMeshProUGUI EnsureSimpleLabel(
        Transform content,
        string name,
        string text,
        float fontSize,
        FontStyles style,
        ref bool changed,
        TextMeshProUGUI existing = null)
    {
        var label = existing != null ? existing : FindDirectLabel(content, name);
        if (label == null)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement), typeof(TextMeshProUGUI));
            go.transform.SetParent(content, false);
            label = go.GetComponent<TextMeshProUGUI>();
            changed = true;
        }

        if (label.name != name)
        {
            label.name = name;
            changed = true;
        }

        if (label.text != text)
        {
            label.text = text;
            changed = true;
        }

        if (Mathf.Abs(label.fontSize - fontSize) > 0.01f)
        {
            label.fontSize = fontSize;
            changed = true;
        }

        if (label.fontStyle != style)
        {
            label.fontStyle = style;
            changed = true;
        }

        if (label.color != new Color(0.85f, 0.85f, 0.85f))
        {
            label.color = new Color(0.85f, 0.85f, 0.85f);
            changed = true;
        }

        if (!label.enableWordWrapping)
        {
            label.enableWordWrapping = true;
            changed = true;
        }

        var le = label.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = label.gameObject.AddComponent<LayoutElement>();
            changed = true;
        }

        float desiredHeight = fontSize >= 24f ? 44f : (fontSize >= 18f ? 28f : 40f);
        if (Mathf.Abs(le.preferredHeight - desiredHeight) > 0.01f)
        {
            le.preferredHeight = desiredHeight;
            changed = true;
        }

        if (le.flexibleWidth != 1f)
        {
            le.flexibleWidth = 1f;
            changed = true;
        }

        return label;
    }

    static TextMeshProUGUI FindDirectLabel(Transform content, string name)
    {
        var direct = content.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var txt in direct)
        {
            if (txt == null || txt.transform.parent != content)
                continue;

            if (txt.name == name)
                return txt;
        }

        return null;
    }

    static bool DisableLegacyDuplicateLabels(Transform content, params TextMeshProUGUI[] keep)
    {
        var keepSet = new HashSet<Transform>();
        for (int i = 0; i < keep.Length; i++)
        {
            if (keep[i] != null)
                keepSet.Add(keep[i].transform);
        }

        bool changed = false;
        var direct = content.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var txt in direct)
        {
            if (txt == null || txt.transform.parent != content)
                continue;

            if (keepSet.Contains(txt.transform))
                continue;

            if (txt.name == "Lbl" || txt.name.Contains("Label") || txt.text.StartsWith("Formula"))
            {
                if (txt.gameObject.activeSelf)
                {
                    txt.gameObject.SetActive(false);
                    changed = true;
                }
            }
        }

        return changed;
    }

    static int PlaceLabeledDropdown(Transform content, TMP_Dropdown dropdown, string labelName, int index, ref bool changed)
    {
        var label = content.Find(labelName);
        index = PlaceBlock(content, label, index, ref changed);
        index = PlaceBlock(content, dropdown != null ? dropdown.transform : null, index, ref changed);
        return index;
    }

    static int PlaceBlock(Transform content, Transform target, int index, ref bool changed)
    {
        if (target == null || target.parent != content)
            return index;

        if (target.GetSiblingIndex() != index)
        {
            target.SetSiblingIndex(index);
            changed = true;
        }

        return index + 1;
    }

    static bool MoveButtonToRow(Button button, Transform row)
    {
        if (button == null || row == null)
            return false;

        bool changed = false;

        if (button.transform.parent != row)
        {
            button.transform.SetParent(row, false);
            changed = true;
        }

        var le = button.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = button.gameObject.AddComponent<LayoutElement>();
            changed = true;
        }

        if (le.preferredHeight != 36f)
        {
            le.preferredHeight = 36f;
            changed = true;
        }

        if (le.flexibleWidth != 1f)
        {
            le.flexibleWidth = 1f;
            changed = true;
        }

        return changed;
    }

    static TMP_Dropdown FindOrCreateDropdown(Transform parent, string objName, string labelText)
    {
        var existing = parent.Find(objName);
        if (existing != null)
        {
            var existingDrop = existing.GetComponent<TMP_Dropdown>();
            if (existingDrop != null)
                return existingDrop;
        }

        return CreateLabeledDropdown(parent, objName, labelText);
    }

    static TMP_Dropdown CreateLabeledDropdown(Transform parent, string objName, string labelText)
    {
        // --- label ---
        GameObject labelGO = new GameObject(objName + "_Label");
        labelGO.transform.SetParent(parent, false);
        var rt = labelGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 24);

        var text = labelGO.AddComponent<TextMeshProUGUI>();
        text.text = labelText;
        text.fontSize = 18;
        text.color = new Color(0.85f, 0.85f, 0.85f);
        text.fontStyle = FontStyles.Bold;

        // --- dropdown ---
        // Clone the first TMP_Dropdown in the parent so structure + fonts match
        TMP_Dropdown source = parent.GetComponentInChildren<TMP_Dropdown>(true);
        GameObject dropGO = Object.Instantiate(source.gameObject, parent);
        dropGO.name = objName;

        // Remove any lingering options from the clone (InitDropdowns will fill them)
        TMP_Dropdown drop = dropGO.GetComponent<TMP_Dropdown>();
        drop.ClearOptions();

        return drop;
    }

    static bool RemoveConflictingTrackedRaycasters(GameObject root)
    {
        int removedTotal = 0;
        string[] fullTypeNames =
        {
            "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit",
            "UnityEngine.InputSystem.UI.TrackedDeviceRaycaster, Unity.InputSystem",
        };

        foreach (var fullTypeName in fullTypeNames)
        {
            var type = System.Type.GetType(fullTypeName);
            if (type == null)
                continue;

            var components = root.GetComponentsInChildren(type, true);
            foreach (var component in components)
            {
                if (component is Component c)
                {
                    Object.DestroyImmediate(c);
                    removedTotal++;
                }
            }
        }

        if (removedTotal > 0)
        {
            Debug.Log($"[PanelSetup] Removed {removedTotal} conflicting tracked raycaster component(s).");
            return true;
        }

        return false;
    }
}
