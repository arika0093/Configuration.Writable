import type {SidebarsConfig} from '@docusaurus/plugin-content-docs';

const sidebars: SidebarsConfig = {
  tutorialSidebar: [
    'intro',
    {
      type: 'category',
      label: 'Getting Started',
      items: [
        'getting-started/installation',
        'getting-started/setup',
      ],
    },
    {
      type: 'category',
      label: 'Usage',
      items: [
        'usage/simple-app',
        'usage/host-app',
        'usage/aspnet-core',
      ],
    },
    {
      type: 'category',
      label: 'Customization',
      items: [
        'customization/configuration',
        'customization/save-location',
        'customization/format-provider',
        'customization/file-provider',
        'customization/change-detection',
        'customization/validation',
        'customization/logging',
        'customization/section-name',
        'customization/register-instance',
      ],
    },
    {
      type: 'category',
      label: 'Advanced',
      items: [
        'advanced/native-aot',
        'advanced/instance-name',
        'advanced/dynamic-options',
        'advanced/clone-strategy',
        'advanced/testing',
      ],
    },
    {
      type: 'category',
      label: 'API Reference',
      items: [
        'api/interfaces',
      ],
    },
  ],
};

export default sidebars;
