import { z } from 'zod';
import { angle, cadTool, entityProps, handle, handles, point, points, type AnyTool, type ToolBridge } from './common.js';

/** Spec §7.3 — blocks and attributes. */
export function blockTools(b: ToolBridge): AnyTool[] {
  return [
    cadTool(b, 'define_block',
      'Create a block definition from existing entities (entity_handles) with base_point as insertion point. By default the originals are replaced by ' +
        'one block reference (replace_with_insert=true, returned in handles). Optional attribute definitions: ' +
        '[{"tag": "SO_HIEU", "position": [x,y], "height": 250, "prompt": "Số hiệu", "default": "D1"}]. ' +
        'Typical: draw a door symbol (leaf + swing arc) with batch_execute, then define_block "CUA_DI_900".',
      {
        name: z.string().min(1),
        base_point: point,
        entity_handles: handles.optional(),
        attributes: z
          .array(z.object({
            tag: z.string().min(1),
            position: point,
            height: z.number().positive().optional(),
            prompt: z.string().optional(),
            default: z.string().optional(),
            justify: z.string().optional(),
            style: z.string().optional(),
            invisible: z.boolean().optional(),
          }))
          .optional(),
        replace_with_insert: z.boolean().optional(),
        overwrite: z.boolean().optional().describe('Redefine an existing block of the same name.'),
        description: z.string().optional(),
      }),

    cadTool(b, 'insert_block',
      'Insert a block reference at position (or many at positions[]), with scale (number or [sx, sy]), rotation (degrees) and attribute values {TAG: value}. ' +
        'Fails with BLOCK_NOT_FOUND if the block is not defined (list_block_defs).',
      {
        name: z.string().min(1),
        position: point.optional(),
        positions: points(1).optional(),
        scale: z.union([z.number().positive(), point]).optional(),
        rotation: angle('Rotation').optional(),
        attributes: z.record(z.string(), z.union([z.string(), z.number()])).optional(),
        ...entityProps,
      }),

    cadTool(b, 'list_block_defs',
      'List block definitions: name, entity count, number of references, attribute tags, dynamic/xref flags.',
      { filter: z.string().optional().describe('Substring or wildcard.') },
      { readOnly: true }),

    cadTool(b, 'get_block_attributes',
      'Read attribute values {TAG: value} of a block reference (and dynamic block properties with allowed values).',
      { handle },
      { readOnly: true }),

    cadTool(b, 'set_block_attributes', 'Set attribute values of a block reference: {"handle": "2F4A", "attributes": {"SO_HIEU": "D2"}}.', {
      handle,
      attributes: z.record(z.string(), z.union([z.string(), z.number()])),
    }),

    cadTool(b, 'set_dynamic_props',
      'Set dynamic block properties (e.g. {"Width": 900, "Flip state": 1, "Visibility1": "Open"}). Distances in mm, angles in degrees. ' +
        'Read available names and allowed values with get_block_attributes first.',
      { handle, props: z.record(z.string(), z.union([z.string(), z.number(), point])) }),
  ];
}
