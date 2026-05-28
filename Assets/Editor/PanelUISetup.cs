using UnityEngine;
using UnityEditor;
using TMPro;
using UnityEngine.UI;
using UnityEditor.SceneManagement;
using System.Collections.Generic;
using VectorFieldUI;

/// <summary>
/// Adds the missing inputScaleX, inputScaleY, and dropZone UI controls
/// to the Panel prefab and wires them to PanelController.
/// Run once: Menu → Panel → Add Missing Dropdowns
/// </summary>
[InitializeOnLoad]
public class PanelUISetup
{
    static bool s_EditModeFixQueued;

    static PanelUISetup()
    {
        QueueEditModePanelFix();
        EditorSceneManager.sceneOpened += (_, __) => QueueEditModePanelFix();
    }

    static void QueueEditModePanelFix()
    {
        if (s_EditModeFixQueued)
            return;

        s_EditModeFixQueued = true;
        EditorApplication.delayCall += RunEditModePanelFix;
    }

    static void RunEditModePanelFix()
    {
        s_EditModeFixQueued = false;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        try
        {
            FixPanelPrefabAll();
            FixPanelInOpenScenes();
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[PanelSetup] No se pudo refrescar el panel en modo edicion: {ex.Message}");
        }
    }

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
        changed |= RemoveLegacyFunctionInputFields(content);

        // Reemplazar input fields por dropdowns
        if (pc.inputScaleX == null)
        {
            pc.inputScaleX = FindOrCreateAxisDropdown(content, "InputScaleX", "Primera funcion f(x,y):");
            changed = true;
            Debug.Log("[PanelSetup] inputScaleX dropdown created/linked.");
        }

        if (pc.inputScaleY == null)
        {
            pc.inputScaleY = FindOrCreateAxisDropdown(content, "InputScaleY", "Segunda funcion f(x,y):");
            changed = true;
            Debug.Log("[PanelSetup] inputScaleY dropdown created/linked.");
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
            var targetSize = new Vector2(Mathf.Max(panelCanvasRt.sizeDelta.x, 480f), 650f);
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
        EnsureDropdownVisualSafety(pc.dropZone, ref changed);

        var vlg = content.GetComponent<VerticalLayoutGroup>();
        if (vlg != null)
        {
            if (vlg.spacing != 2f)
            {
                vlg.spacing = 2f;
                changed = true;
            }

            if (vlg.padding.top != 4 || vlg.padding.bottom != 12 || vlg.padding.left != 12 || vlg.padding.right != 12)
            {
                vlg.padding.top = 4;
                vlg.padding.bottom = 12;
                vlg.padding.left = 12;
                vlg.padding.right = 12;
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
        if (descriptionLabel != null && descriptionLabel.gameObject.activeSelf)
        {
            descriptionLabel.gameObject.SetActive(false);
            changed = true;
        }

        var sectionFunctions = EnsureSimpleLabel(content, "FunctionsSectionLabel", "Funciones:", 20f, FontStyles.Bold, ref changed);

        var countLabel = EnsureLabelForDropdown(content, pc.dropCount, "CountFunctionLabel", "Cantidad de Vectores", ref changed);
        var countDesc = EnsureDescriptionSmall(content, "CountDesc", "Elige cuántos vectores mostrar en el campo", ref changed);
        
        var formulaLabel = EnsureLabelForDropdown(content, pc.dropFormula, "FormulaFunctionLabel", "Fórmula", ref changed);
        var formulaDesc = EnsureDescriptionSmall(content, "FormulaDesc", "Selecciona el patrón de dirección de los vectores", ref changed);
        
        var scaleXLabel = EnsureLabelForDropdown(content, pc.inputScaleX, "InputScaleX_Label", "Primera funcion f(x,y):", ref changed);
        var scaleXDesc = EnsureDescriptionSmall(content, "ScaleXDesc", "Opciones: X, -X, -Y, Y, -X-Y", ref changed);
        
        var scaleYLabel = EnsureLabelForDropdown(content, pc.inputScaleY, "InputScaleY_Label", "Segunda funcion f(x,y):", ref changed);
        var scaleYDesc = EnsureDescriptionSmall(content, "ScaleYDesc", "Opciones: Y, -Y, X, -X, x-y", ref changed);

        var zoneLabel = EnsureLabelForDropdown(content, pc.dropZone, "DropdownZone_Label", "Zona", ref changed);
        var zoneDesc = EnsureDescriptionSmall(content, "ZoneDesc", "Selecciona la zona donde aplicar el campo", ref changed);
        changed |= RemoveDuplicateDirectLabels(content, scaleXLabel, scaleYLabel);

        int insertIndex = 0;
        insertIndex = PlaceBlock(content, titleLabel?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, descriptionLabel?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, sectionFunctions?.transform, insertIndex, ref changed);

        insertIndex = PlaceBlock(content, countLabel?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, pc.dropCount?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, countDesc?.transform, insertIndex, ref changed);

        insertIndex = PlaceBlock(content, formulaLabel?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, pc.dropFormula?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, formulaDesc?.transform, insertIndex, ref changed);

        // Funcion 1
        insertIndex = PlaceBlock(content, scaleXLabel?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, pc.inputScaleX?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, scaleXDesc?.transform, insertIndex, ref changed);

        // Funcion 2
        insertIndex = PlaceBlock(content, scaleYLabel?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, pc.inputScaleY?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, scaleYDesc?.transform, insertIndex, ref changed);

        // Zona
        insertIndex = PlaceBlock(content, zoneLabel?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, pc.dropZone?.transform, insertIndex, ref changed);
        insertIndex = PlaceBlock(content, zoneDesc?.transform, insertIndex, ref changed);

        var spacer = content.Find("ButtonsSpacer");
        if (spacer == null)
        {
            var spacerGO = new GameObject("ButtonsSpacer", typeof(RectTransform), typeof(LayoutElement));
            spacerGO.transform.SetParent(content, false);
            spacer = spacerGO.transform;
            changed = true;
        }

        var spacerLE = spacer.GetComponent<LayoutElement>();
        if (spacerLE.preferredHeight != 8f)
        {
            spacerLE.preferredHeight = 8f;
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
        EnsureGenerateButtonStyle(pc.btnGenerate, ref changed);

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

        FixDropdownTemplate(dropdown);

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

            if (rt.sizeDelta.y < 36f)
            {
                rt.sizeDelta = new Vector2(rt.sizeDelta.x, 36f);
                changed = true;
            }
        }

        var le = dropdown.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = dropdown.gameObject.AddComponent<LayoutElement>();
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
    }

    static void FixDropdownTemplate(TMP_Dropdown dropdown)
    {
        if (dropdown == null)
            return;

        if (dropdown.template == null)
        {
            var template = dropdown.transform.Find("Template") as RectTransform;
            if (template == null)
            {
                var rects = dropdown.GetComponentsInChildren<RectTransform>(true);
                for (int i = 0; i < rects.Length; i++)
                {
                    if (rects[i] != null && rects[i].name == "Template")
                    {
                        template = rects[i];
                        break;
                    }
                }
            }

            dropdown.template = template;
        }

        if (dropdown.template != null)
        {
            if (dropdown.template.GetComponent<CanvasGroup>() == null)
                dropdown.template.gameObject.AddComponent<CanvasGroup>();

            dropdown.template.gameObject.SetActive(false);
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

        title = EnsureSimpleLabel(content, "PanelTitleLabel", "CAMPO VECTORIAL", 42f, FontStyles.Bold, ref changed, title);
        
        // Asegurar color azul claro brillante para el título
        if (title.color != new Color(0.08f, 0.56f, 1f))
        {
            title.color = new Color(0.08f, 0.56f, 1f);
            changed = true;
        }

        // Asegurar buen tamaño en el layout
        var le = title.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = title.gameObject.AddComponent<LayoutElement>();
            changed = true;
        }

        if (le.preferredHeight != 48f)
        {
            le.preferredHeight = 48f;
            changed = true;
        }

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

        if (!label.gameObject.activeSelf)
        {
            label.gameObject.SetActive(true);
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

        if (label.textWrappingMode == TextWrappingModes.NoWrap)
        {
            label.textWrappingMode = TextWrappingModes.Normal;
            changed = true;
        }

        var le = label.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = label.gameObject.AddComponent<LayoutElement>();
            changed = true;
        }

        float desiredHeight = fontSize >= 24f ? 44f : (fontSize >= 18f ? 28f : 20f);
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

    static TextMeshProUGUI EnsureDescriptionSmall(Transform content, string name, string text, ref bool changed)
    {
        var label = FindDirectLabel(content, name);
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

        float fontSize = 12f;
        if (Mathf.Abs(label.fontSize - fontSize) > 0.01f)
        {
            label.fontSize = fontSize;
            changed = true;
        }

        // Color gris más tenue para descripciones
        Color descColor = new Color(0.65f, 0.65f, 0.65f, 0.9f);
        if (label.color != descColor)
        {
            label.color = descColor;
            changed = true;
        }

        if (label.fontStyle != FontStyles.Italic)
        {
            label.fontStyle = FontStyles.Italic;
            changed = true;
        }

        if (label.textWrappingMode == TextWrappingModes.NoWrap)
        {
            label.textWrappingMode = TextWrappingModes.Normal;
            changed = true;
        }

        var le = label.GetComponent<LayoutElement>();
        if (le == null)
        {
            le = label.gameObject.AddComponent<LayoutElement>();
            changed = true;
        }

        if (Mathf.Abs(le.preferredHeight - 12f) > 0.01f)
        {
            le.preferredHeight = 12f;
            changed = true;
        }

        if (le.flexibleWidth != 1f)
        {
            le.flexibleWidth = 1f;
            changed = true;
        }

        return label;
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

    static void EnsureGenerateButtonStyle(Button generateButton, ref bool changed)
    {
        if (generateButton == null)
            return;

        var text = generateButton.GetComponentInChildren<TextMeshProUGUI>(true);
        if (text != null)
        {
            if (text.text != "Generar Campo Vectorial")
            {
                text.text = "Generar Campo Vectorial";
                changed = true;
            }

            if (text.color != Color.white)
            {
                text.color = Color.white;
                changed = true;
            }

            if (text.fontStyle != FontStyles.Bold)
            {
                text.fontStyle = FontStyles.Bold;
                changed = true;
            }
        }

        var image = generateButton.GetComponent<Image>();
        if (image != null)
        {
            Color normal = new Color(0.12f, 0.70f, 0.26f, 1f);
            if (image.color != normal)
            {
                image.color = normal;
                changed = true;
            }

            var colors = generateButton.colors;
            bool colorsChanged = false;

            if (colors.normalColor != normal)
            {
                colors.normalColor = normal;
                colorsChanged = true;
            }

            var highlighted = new Color(0.18f, 0.78f, 0.33f, 1f);
            if (colors.highlightedColor != highlighted)
            {
                colors.highlightedColor = highlighted;
                colorsChanged = true;
            }

            var pressed = new Color(0.08f, 0.58f, 0.20f, 1f);
            if (colors.pressedColor != pressed)
            {
                colors.pressedColor = pressed;
                colorsChanged = true;
            }

            if (colors.selectedColor != highlighted)
            {
                colors.selectedColor = highlighted;
                colorsChanged = true;
            }

            if (colors.colorMultiplier != 1f)
            {
                colors.colorMultiplier = 1f;
                colorsChanged = true;
            }

            if (colorsChanged)
            {
                generateButton.colors = colors;
                changed = true;
            }
        }
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

     static TMP_Dropdown FindOrCreateAxisDropdown(Transform parent, string objName, string labelText)
    {
        var existingDrop = FindDirectDropdown(parent, objName);
        if (existingDrop != null)
            return existingDrop;

        var existing = parent.Find(objName);
        if (existing != null && existing.GetComponent<TMP_InputField>() == null)
        {
            existingDrop = existing.GetComponent<TMP_Dropdown>();
            if (existingDrop != null)
                return existingDrop;
        }

        return CreateAxisDropdown(parent, objName, labelText);
    }

    static TMP_Dropdown FindDirectDropdown(Transform parent, string objName)
    {
        if (parent == null)
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            var child = parent.GetChild(i);
            if (child == null || child.name != objName)
                continue;

            var dropdown = child.GetComponent<TMP_Dropdown>();
            if (dropdown != null)
                return dropdown;
        }

        return null;
    }

    static TMP_Dropdown CreateAxisDropdown(Transform parent, string objName, string labelText)
    {
        // label
        GameObject labelGO = new GameObject(objName + "_Label");
        labelGO.transform.SetParent(parent, false);
        var rt = labelGO.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 24);

        var text = labelGO.AddComponent<TextMeshProUGUI>();
        text.text = labelText;
        text.fontSize = 18;
        text.color = new Color(0.85f, 0.85f, 0.85f);
        text.fontStyle = FontStyles.Bold;

        // dropdown: clone first dropdown in parent so structure matches
        TMP_Dropdown source = parent.GetComponentInChildren<TMP_Dropdown>(true);
        if (source == null)
        {
            Debug.LogError("[PanelSetup] Source TMP_Dropdown not found while creating axis dropdown.", parent);
            return null;
        }
        GameObject dropGO = Object.Instantiate(source.gameObject, parent);
        dropGO.name = objName;

        TMP_Dropdown drop = dropGO.GetComponent<TMP_Dropdown>();
        drop.ClearOptions();
        drop.value = 0;

        string[] firstFuncOpts  = { "X", "-X", "-Y", "Y", "-X-Y" };
        string[] secondFuncOpts = { "Y", "-Y", "X", "-X", "X-Y" };
        string[] opts = objName == "InputScaleX" ? firstFuncOpts : secondFuncOpts;
        foreach (var o in opts)
            drop.options.Add(new TMP_Dropdown.OptionData(o));

        FixDropdownTemplate(drop);
        return drop;
    }

    static bool RemoveLegacyFunctionInputFields(Transform content)
    {
        if (content == null)
            return false;

        bool changed = false;
        string[] legacyNames = { "InputScaleX", "InputScaleY" };

        for (int i = content.childCount - 1; i >= 0; i--)
        {
            var child = content.GetChild(i);
            if (child == null)
                continue;

            bool isLegacyName = false;
            for (int n = 0; n < legacyNames.Length; n++)
            {
                if (child.name == legacyNames[n])
                {
                    isLegacyName = true;
                    break;
                }
            }

            if (!isLegacyName)
                continue;

            if (child.GetComponent<TMP_InputField>() == null)
                continue;

            Object.DestroyImmediate(child.gameObject);
            changed = true;
        }

        return changed;
    }

    static bool RemoveDuplicateDirectLabels(Transform content, params TextMeshProUGUI[] keep)
    {
        if (content == null)
            return false;

        var keepSet = new HashSet<TextMeshProUGUI>();
        for (int i = 0; i < keep.Length; i++)
        {
            if (keep[i] != null)
                keepSet.Add(keep[i]);
        }

        bool changed = false;
        string[] targetNames = { "InputScaleX_Label", "InputScaleY_Label" };
        var labels = content.GetComponentsInChildren<TextMeshProUGUI>(true);
        for (int i = labels.Length - 1; i >= 0; i--)
        {
            var label = labels[i];
            if (label == null || label.transform.parent != content || keepSet.Contains(label))
                continue;

            bool isTarget = false;
            for (int n = 0; n < targetNames.Length; n++)
            {
                if (label.name == targetNames[n])
                {
                    isTarget = true;
                    break;
                }
            }

            if (!isTarget)
                continue;

            Object.DestroyImmediate(label.gameObject);
            changed = true;
        }

        return changed;
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
            "Unity.XR.Interaction.Toolkit",
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