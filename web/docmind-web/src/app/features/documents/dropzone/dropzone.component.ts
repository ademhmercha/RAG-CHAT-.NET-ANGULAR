import { ChangeDetectionStrategy, Component, output, signal } from '@angular/core';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { TranslocoPipe } from '@jsverse/transloco';
import { ACCEPT_ATTRIBUTE, MAX_UPLOAD_BYTES } from '../util/upload-validation';

@Component({
  selector: 'dm-dropzone',
  imports: [MatButtonModule, MatIconModule, TranslocoPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './dropzone.component.html',
  styleUrl: './dropzone.component.scss',
})
export class DropzoneComponent {
  readonly filesSelected = output<File[]>();

  protected readonly dragging = signal(false);
  protected readonly accept = ACCEPT_ATTRIBUTE;
  protected readonly maxSizeMb = Math.round(MAX_UPLOAD_BYTES / (1024 * 1024));

  private depth = 0;

  protected onDragEnter(event: DragEvent): void {
    if (!hasFiles(event)) {
      return;
    }

    event.preventDefault();
    this.depth++;
    this.dragging.set(true);
  }

  protected onDragOver(event: DragEvent): void {
    if (hasFiles(event)) {
      event.preventDefault();
    }
  }

  protected onDragLeave(): void {
    this.depth = Math.max(0, this.depth - 1);
    if (this.depth === 0) {
      this.dragging.set(false);
    }
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.depth = 0;
    this.dragging.set(false);

    const files = Array.from(event.dataTransfer?.files ?? []);
    if (files.length > 0) {
      this.filesSelected.emit(files);
    }
  }

  protected onPick(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';

    if (files.length > 0) {
      this.filesSelected.emit(files);
    }
  }
}

function hasFiles(event: DragEvent): boolean {
  return event.dataTransfer?.types.includes('Files') ?? false;
}
