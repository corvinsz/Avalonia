﻿// Copyright (c) The Perspex Project. All rights reserved.
// Licensed under the MIT license. See licence.md file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using Moq;
using Perspex.Controls.Presenters;
using Perspex.Controls.Primitives;
using Perspex.Controls.Templates;
using Perspex.LogicalTree;
using Perspex.Styling;
using Perspex.VisualTree;
using Xunit;

namespace Perspex.Controls.UnitTests.Primitives
{
    public class TemplatedControlTests
    {
        [Fact]
        public void Template_Doesnt_Get_Executed_On_Set()
        {
            bool executed = false;

            var template = new FuncControlTemplate(_ =>
            {
                executed = true;
                return new Control();
            });

            var target = new TemplatedControl
            {
                Template = template,
            };

            Assert.False(executed);
        }

        [Fact]
        public void Template_Gets_Executed_On_Measure()
        {
            bool executed = false;

            var template = new FuncControlTemplate(_ =>
            {
                executed = true;
                return new Control();
            });

            var target = new TemplatedControl
            {
                Template = template,
            };

            target.Measure(new Size(100, 100));

            Assert.True(executed);
        }

        [Fact]
        public void ApplyTemplate_Should_Create_Visual_Children()
        {
            var target = new TemplatedControl
            {
                Template = new FuncControlTemplate(_ => new Decorator
                {
                    Child = new Panel
                    {
                        Children = new Controls
                        {
                            new TextBlock(),
                            new Border(),
                        }
                    }
                }),
            };

            target.ApplyTemplate();

            var types = target.GetVisualDescendents().Select(x => x.GetType()).ToList();

            Assert.Equal(
                new[]
                {
                    typeof(Decorator),
                    typeof(Panel),
                    typeof(TextBlock),
                    typeof(Border)
                },
                types);
            Assert.Empty(target.GetLogicalChildren());
        }

        [Fact]
        public void Templated_Child_Should_Be_NameScope()
        {
            var target = new TemplatedControl
            {
                Template = new FuncControlTemplate(_ => new Decorator
                {
                    Child = new Panel
                    {
                        Children = new Controls
                        {
                            new TextBlock(),
                            new Border(),
                        }
                    }
                }),
            };

            target.ApplyTemplate();

            Assert.NotNull(NameScope.GetNameScope((Control)target.GetVisualChildren().Single()));
        }

        [Fact]
        public void Templated_Children_Should_Have_TemplatedParent_Set()
        {
            var target = new TemplatedControl
            {
                Template = new FuncControlTemplate(_ => new Decorator
                {
                    Child = new Panel
                    {
                        Children = new Controls
                        {
                            new TextBlock(),
                            new Border(),
                        }
                    }
                }),
            };

            target.ApplyTemplate();

            var templatedParents = target.GetVisualDescendents()
                .OfType<IControl>()
                .Select(x => x.TemplatedParent)
                .ToList();

            Assert.Equal(4, templatedParents.Count);
            Assert.True(templatedParents.All(x => x == target));
        }

        [Fact]
        public void Templated_Child_Should_Have_Parent_Set()
        {
            var target = new TemplatedControl
            {
                Template = new FuncControlTemplate(_ => new Decorator())
            };

            target.ApplyTemplate();

            var child = (Decorator)target.GetVisualChildren().Single();

            Assert.Equal(target, child.Parent);
            Assert.Equal(target, child.GetLogicalParent());
        }

        [Fact]
        public void Nested_Templated_Controls_Have_Correct_TemplatedParent()
        {
            var target = new TestTemplatedControl
            {
                Template = new FuncControlTemplate(_ =>
                {
                    return new ContentControl
                    {
                        Template = new FuncControlTemplate(parent =>
                        {
                            return new Border
                            {
                                Child = new ContentPresenter
                                {
                                    [~ContentPresenter.ContentProperty] = parent.GetObservable(ContentControl.ContentProperty),
                                }
                            };
                        }),
                        Content = new TextBlock
                        {
                        }
                    };
                }),
            };

            target.ApplyTemplate();

            var contentControl = target.GetTemplateChildren().OfType<ContentControl>().Single();
            var border = contentControl.GetTemplateChildren().OfType<Border>().Single();
            var presenter = contentControl.GetTemplateChildren().OfType<ContentPresenter>().Single();
            var textBlock = (TextBlock)presenter.Content;

            Assert.Equal(target, contentControl.TemplatedParent);
            Assert.Equal(contentControl, border.TemplatedParent);
            Assert.Equal(contentControl, presenter.TemplatedParent);
            Assert.Equal(target, textBlock.TemplatedParent);
        }

        [Fact]
        public void Templated_Children_Should_Be_Styled()
        {
            using (PerspexLocator.EnterScope())
            {
                var styler = new Mock<IStyler>();

                PerspexLocator.CurrentMutable.Bind<IStyler>().ToConstant(styler.Object);

                TestTemplatedControl target;

                var root = new TestRoot
                {
                    Child = target = new TestTemplatedControl
                    {
                        Template = new FuncControlTemplate(_ =>
                        {
                            return new StackPanel
                            {
                                Children = new Controls
                            {
                                new TextBlock
                                {
                                }
                            }
                            };
                        }),
                    }
                };

                target.ApplyTemplate();

                styler.Verify(x => x.ApplyStyles(It.IsAny<TestTemplatedControl>()), Times.Once());
                styler.Verify(x => x.ApplyStyles(It.IsAny<StackPanel>()), Times.Once());
                styler.Verify(x => x.ApplyStyles(It.IsAny<TextBlock>()), Times.Once());
            }
        }

        [Fact]
        public void Nested_TemplatedControls_Should_Register_With_Correct_NameScope()
        {
            var target = new ContentControl
            {
                Template = new FuncControlTemplate<ContentControl>(ScrollingContentControlTemplate),
                Content = "foo"
            };

            target.ApplyTemplate();

            var border = target.GetVisualChildren().FirstOrDefault();
            Assert.IsType<Border>(border);
            var scrollViewer = border.GetVisualChildren().FirstOrDefault();
            Assert.IsType<ScrollViewer>(scrollViewer);
            var scrollContentPresenter = scrollViewer.GetVisualChildren().FirstOrDefault();
            Assert.IsType<ScrollContentPresenter>(scrollContentPresenter);
            var contentPresenter = scrollContentPresenter.GetVisualChildren().FirstOrDefault();
            Assert.IsType<ContentPresenter>(contentPresenter);

            var borderNs = NameScope.GetNameScope((Control)border);
            var scrollContentPresenterNs = NameScope.GetNameScope((Control)scrollContentPresenter);
            Assert.NotNull(borderNs);
            Assert.Same(scrollViewer, borderNs.Find("ScrollViewer"));
            Assert.Same(contentPresenter, borderNs.Find("PART_ContentPresenter"));
            Assert.Same(scrollContentPresenter, scrollContentPresenterNs.Find("PART_ContentPresenter"));
        }

        [Fact]
        public void ApplyTemplate_Should_Raise_TemplateApplied()
        {
            var target = new TestTemplatedControl
            {
                Template = new FuncControlTemplate(_ => new Decorator())
            };

            var raised = false;

            target.TemplateApplied += (s, e) =>
            {
                Assert.Equal(TemplatedControl.TemplateAppliedEvent, e.RoutedEvent);
                Assert.Same(target, e.Source);
                Assert.NotNull(e.NameScope);
                raised = true;
            };

            target.ApplyTemplate();

            Assert.True(raised);
        }

        private static IControl ScrollingContentControlTemplate(ContentControl control)
        {
            return new Border
            {
                Child = new ScrollViewer
                {
                    Template = new FuncControlTemplate<ScrollViewer>(ScrollViewerTemplate),
                    Name = "ScrollViewer",
                    Content = new ContentPresenter
                    {
                        Name = "PART_ContentPresenter",
                        [!ContentPresenter.ContentProperty] = control[!ContentControl.ContentProperty],
                    }
                }
            };
        }

        private static IControl ItemsControlTemplate(ItemsControl control)
        {
            return new Border
            {
                Child = new ScrollViewer
                {
                    Template = new FuncControlTemplate<ScrollViewer>(ScrollViewerTemplate),
                    Content = new ItemsPresenter
                    {
                        Name = "PART_ItemsPresenter",
                        [!ItemsPresenter.ItemsProperty] = control[!ItemsControl.ItemsProperty],
                        [!ItemsPresenter.ItemsPanelProperty] = control[!ItemsControl.ItemsPanelProperty],
                    }
                }
            };
        }

        private static Control ScrollViewerTemplate(ScrollViewer control)
        {
            var result = new ScrollContentPresenter
            {
                Name = "PART_ContentPresenter",
                [~ContentPresenter.ContentProperty] = control[~ContentControl.ContentProperty],
            };

            return result;
        }

        private class ApplyTemplateTracker : Control
        {
            public List<Tuple<IVisual, ILogical>> Invocations { get; } = new List<Tuple<IVisual, ILogical>>();

            public override void ApplyTemplate()
            {
                base.ApplyTemplate();
                Invocations.Add(Tuple.Create(this.GetVisualParent(), this.GetLogicalParent()));
            }
        }
    }
}