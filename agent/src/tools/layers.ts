import { z } from 'zod';
import { cadTool, type AnyTool, type ToolBridge } from './common.js';

const lineweight = z
  .union([z.number().min(0).max(211), z.enum(['default', 'bylayer'])])
  .describe('Lineweight in mm, snapped to the nearest AutoCAD value: 0.09 0.13 0.15 0.18 0.2 0.25 0.3 0.35 0.4 0.5 0.6 0.7 0.8 1.0 ... or "default".');

const aci = z.number().int().min(1).max(255).describe('ACI color 1-255 (1 red, 2 yellow, 3 green, 4 cyan, 5 blue, 6 magenta, 7 white, 8/9 greys).');

/** Spec §7.4 — layers. */
export function layerTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'create_layer',
      'Create a layer (idempotent: if it exists, the given properties are applied to it). Missing linetypes are loaded from acadiso.lin automatically. ' +
        'Example: {"name": "A-WALL", "color": 7, "lineweight": 0.5}. Returns the layer properties and {"created": true|false}.',
      {
        name: z.string().min(1).describe('Layer name, e.g. "A-WALL", "A-AXIS", "E-LIGHT".'),
        color: aci.optional(),
        linetype: z.string().optional().describe('e.g. "CONTINUOUS", "CENTER" (axes), "DASHED", "HIDDEN".'),
        lineweight: lineweight.optional(),
        plottable: z.boolean().optional().describe('Default true.'),
        description: z.string().optional(),
      }),

    cadTool(b, 'list_layers',
      'List layers with color, linetype, lineweight (mm), on/frozen/locked/plottable and which is current. Call before drawing to reuse existing layers.',
      { filter: z.string().optional().describe('Optional substring or wildcard (e.g. "A-*") to filter names.') },
      { readOnly: true }),

    cadTool(b, 'set_current_layer', 'Make a layer current (new entities without "layer" go there). Thaws it if frozen.', {
      name: z.string().min(1),
    }),

    cadTool(b, 'set_layer_props',
      'Change layer properties: color, linetype, lineweight, on (visible), frozen, locked, plottable, or rename with new_name. ' +
        'The current layer cannot be frozen. Unlocking or turning on layers the user locked/hid should be confirmed with the user first.',
      {
        name: z.string().min(1),
        new_name: z.string().optional(),
        color: aci.optional(),
        linetype: z.string().optional(),
        lineweight: lineweight.optional(),
        on: z.boolean().optional(),
        frozen: z.boolean().optional(),
        locked: z.boolean().optional(),
        plottable: z.boolean().optional(),
        description: z.string().optional(),
      }),
  ];
}
