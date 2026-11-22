import { bootstrapApplication } from '@angular/platform-browser';
import { App } from './app/app';
import { buildAppConfig } from './app/app.config';
import { loadAppConfig } from './app/core/config/app-config';
import { showStartupFailure } from './app/core/config/startup-failure';

loadAppConfig()
  .then((config) => bootstrapApplication(App, buildAppConfig(config)))
  .catch((error: unknown) => showStartupFailure(document, error));
