import { decodeDocumentEvent } from './document-events.service';

describe('decodeDocumentEvent', () => {
  it('decodes a status change, whatever the casing of the status', () => {
    const event = decodeDocumentEvent(
      'status',
      '{"documentId":"d1","status":"ready","errorMessage":null,"updatedAt":"2030-01-01T00:00:00Z"}',
    );

    expect(event).toEqual({
      type: 'status',
      documentId: 'd1',
      status: 'Ready',
      errorMessage: null,
      updatedAt: '2030-01-01T00:00:00Z',
    });
  });

  it('keeps the failure reason', () => {
    const event = decodeDocumentEvent(
      'status',
      '{"documentId":"d1","status":"Failed","errorMessage":"Corrupt file"}',
    );

    expect(event).toMatchObject({ status: 'Failed', errorMessage: 'Corrupt file' });
  });

  it('ignores unknown events, bad payloads and unknown statuses', () => {
    expect(decodeDocumentEvent('other', '{}')).toBeNull();
    expect(decodeDocumentEvent('status', 'nope')).toBeNull();
    expect(decodeDocumentEvent('status', '{"documentId":"d1","status":"Weird"}')).toBeNull();
    expect(decodeDocumentEvent('status', '{"status":"Ready"}')).toBeNull();
  });
});
