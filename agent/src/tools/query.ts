import { cadTool, type AnyTool, type ToolBridge } from './common.js';

/** Spec §7.6 — reading and querying (more tools added in phase 1c). */
export function queryTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'get_drawing_info',
      'Read the state of the open drawing: file name, units (should be millimeters), entity count by type and by layer, extents (min/max), ' +
        'current layer / text style / dim style, layer and block counts, LTSCALE, DIMSCALE. Call this FIRST at the start of every task.',
      {},
      { readOnly: true }),
  ];
}
